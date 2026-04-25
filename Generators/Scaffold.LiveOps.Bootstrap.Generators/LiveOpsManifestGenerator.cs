using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Scaffold.LiveOps.Bootstrap.Generators
{
    [Generator]
    public sealed class LiveOpsManifestGenerator : IIncrementalGenerator
    {
        private static readonly DiagnosticDescriptor s_duplicateWire = new(
            id: "LOPSKEY001",
            title: "Duplicate GameApi wire key",
            messageFormat: "Duplicate wire key '{0}' for types: {1}",
            category: "LiveOps",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_missingGeneratorRef = new(
            id: "LOPSKEY002",
            title: "LiveOps key map missing in DTO assembly",
            messageFormat: "Assembly '{0}' contains [LiveOpsKey] types but has no generated LiveOpsKeyRuntimeMap; add a ProjectReference to Scaffold.LiveOps.Bootstrap.Generators with OutputItemType=\"Analyzer\" ReferenceOutputAssembly=\"false\".",
            category: "LiveOps",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(
                context.CompilationProvider,
                (sp, compilation) => Execute(sp, compilation));
        }

        private static void Execute(SourceProductionContext context, Compilation compilation)
        {
            INamedTypeSymbol? iGameHandler2 = compilation.GetTypeByMetadataName("LiveOps.GameApi.IGameApiHandler`2");
            INamedTypeSymbol? iModule = compilation.GetTypeByMetadataName("LiveOps.GameModule.IGameModule");
            INamedTypeSymbol? keyAttr = compilation.GetTypeByMetadataName("LiveOps.DTO.Keys.LiveOpsKeyAttribute");
            INamedTypeSymbol? gameApiAttr = compilation.GetTypeByMetadataName("LiveOps.DTO.Keys.GameApiRequestAttribute");
            INamedTypeSymbol? moduleRequest = compilation.GetTypeByMetadataName("LiveOps.DTO.ModuleRequest.ModuleRequest");
            INamedTypeSymbol? iGameModuleData = compilation.GetTypeByMetadataName("LiveOps.DTO.GameModule.IGameModuleData");

            var types = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            VisitAssembly(compilation.Assembly, types);
            foreach (MetadataReference reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol refSym)
                {
                    continue;
                }

                if (IsRelevantRef(refSym))
                {
                    VisitAssembly(refSym, types);
                }
            }

            List<KeyTypeRecord> keyRecords = BuildKeyTypeRecords(
                types,
                keyAttr,
                gameApiAttr,
                moduleRequest,
                iGameModuleData,
                iGameHandler2,
                iModule);

            EmitLiveOpsKeysCatalog(context, compilation, keyRecords, keyAttr);
            EmitRuntimeKeyMap(context, compilation, keyRecords);
            ReportWireKeyCollisionsIfAny(context, keyRecords, moduleRequest);
            ReportMissingKeyMapInDtoAssembliesIfAny(context, compilation, keyRecords);

            if (iGameHandler2 is null && iModule is null)
            {
                if (string.Equals(compilation.Assembly.Name, "LiveOps", StringComparison.Ordinal))
                {
                    EmitEmpty(context);
                }

                return;
            }

            // Manifest is owned by the Cloud Code deploy shell only (avoids emitting LiveOpsManifest into test / tool projects that reference this generator).
            if (!string.Equals(compilation.Assembly.Name, "LiveOps", StringComparison.Ordinal))
            {
                return;
            }

            var manifestEntries = new List<Entry>(keyRecords.Count);
            foreach (KeyTypeRecord r in keyRecords)
            {
                if (r.Type.IsAbstract)
                {
                    continue;
                }

                if (r.Type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
                {
                    continue;
                }

                if (r.IsGameApiHandler || r.IsGameModule)
                {
                    manifestEntries.Add(
                        new Entry(
                            r.Type,
                            r.IsGameApiHandler,
                            r.IsGameModule,
                            r.IsGameApiHandler ? r.RequestType : null,
                            r.IsGameApiHandler ? r.ResponseType : null));
                }
            }

            Emit(context, manifestEntries);
        }

        private static List<KeyTypeRecord> BuildKeyTypeRecords(
            IReadOnlyCollection<INamedTypeSymbol> types,
            INamedTypeSymbol? keyAttr,
            INamedTypeSymbol? gameApiAttr,
            INamedTypeSymbol? moduleRequest,
            INamedTypeSymbol? iGameModuleData,
            INamedTypeSymbol? iGameHandler2,
            INamedTypeSymbol? iModule)
        {
            var list = new List<KeyTypeRecord>(types.Count);
            foreach (INamedTypeSymbol t in types)
            {
                if (t is null)
                {
                    continue;
                }

                string moduleKey = ResolveModuleKey(t, keyAttr);
                bool isModuleRequestSubtype = moduleRequest is not null && InheritsFrom(t, moduleRequest);
                bool isGameModuleData = iGameModuleData is not null && ImplementsInterface(t, iGameModuleData);
                string? wireKey = ResolveWireKeyNullable(t, gameApiAttr, moduleRequest);
                bool hasLiveOpsKey = HasLiveOpsKeyAttribute(t, keyAttr);

                bool isHandler = false;
                bool isMod = false;
                ITypeSymbol? req = null;
                ITypeSymbol? res = null;
                if (!t.IsAbstract && t.TypeKind is TypeKind.Class or TypeKind.Struct)
                {
                    if (iGameHandler2 is not null)
                    {
                        isHandler = TryGetHandlerArgs(t, iGameHandler2, out req, out res);
                    }

                    if (iModule is not null)
                    {
                        isMod = ImplementsInterface(t, iModule);
                    }
                }

                list.Add(
                    new KeyTypeRecord(
                        t,
                        moduleKey,
                        wireKey,
                        hasLiveOpsKey,
                        isModuleRequestSubtype,
                        isGameModuleData,
                        isHandler,
                        isMod,
                        req,
                        res));
            }

            return list;
        }

        private static bool HasLiveOpsKeyAttribute(INamedTypeSymbol t, INamedTypeSymbol? keyAttr)
        {
            if (keyAttr is null || t.GetAttributes().IsDefaultOrEmpty)
            {
                return false;
            }

            foreach (AttributeData ad in t.GetAttributes())
            {
                if (ad.AttributeClass is null || !SymbolEqualityComparer.Default.Equals(ad.AttributeClass, keyAttr) ||
                    ad.ConstructorArguments.Length < 1)
                {
                    continue;
                }

                if (ad.ConstructorArguments[0].Value is string v && !string.IsNullOrEmpty(v))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveModuleKey(INamedTypeSymbol t, INamedTypeSymbol? keyAttr)
        {
            if (keyAttr is not null)
            {
                foreach (AttributeData ad in t.GetAttributes())
                {
                    if (ad.AttributeClass is null || !SymbolEqualityComparer.Default.Equals(ad.AttributeClass, keyAttr) ||
                        ad.ConstructorArguments.Length < 1)
                    {
                        continue;
                    }

                    if (ad.ConstructorArguments[0].Value is string value && !string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }
            }

            return t.Name;
        }

        /// <summary>
        /// Wire key only for GameApi / ModuleRequest; null for persistence, config, and other storage-only DTOs
        /// (avoids duplicating the module/storage string as a fake wire key).
        /// </summary>
        private static string? ResolveWireKeyNullable(
            INamedTypeSymbol t,
            INamedTypeSymbol? gameApiAttr,
            INamedTypeSymbol? moduleRequest)
        {
            if (gameApiAttr is not null)
            {
                foreach (AttributeData ad in t.GetAttributes())
                {
                    if (ad.AttributeClass is null || !SymbolEqualityComparer.Default.Equals(ad.AttributeClass, gameApiAttr) ||
                        ad.ConstructorArguments.Length < 1)
                    {
                        continue;
                    }

                    if (ad.ConstructorArguments[0].Value is string s && !string.IsNullOrEmpty(s))
                    {
                        return s;
                    }
                }
            }

            if (moduleRequest is not null && InheritsFrom(t, moduleRequest))
            {
                return t.Name;
            }

            return null;
        }

        private static void EmitLiveOpsKeysCatalog(
            SourceProductionContext context,
            Compilation compilation,
            IReadOnlyList<KeyTypeRecord> keyRecords,
            INamedTypeSymbol? keyAttr)
        {
            if (keyAttr is null)
            {
                return;
            }

            if (!string.Equals(compilation.Assembly.Name, "LiveOps", StringComparison.Ordinal))
            {
                return;
            }

            var valueToIdentifier = new Dictionary<string, string>(StringComparer.Ordinal);
            var usedConstNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyTypeRecord r in keyRecords)
            {
                if (!r.HasLiveOpsKeyAttribute)
                {
                    continue;
                }

                // Const catalog: snapshot modules (GameData) and request types only — not per-slot persistence/config keys.
                if (!r.IsModuleRequestSubtype && !r.IsGameModuleData)
                {
                    continue;
                }

                string value = r.ModuleKey;
                if (string.IsNullOrEmpty(value) || valueToIdentifier.ContainsKey(value))
                {
                    continue;
                }

                valueToIdentifier[value] = MakeUniqueConstIdentifier(value, usedConstNames);
            }

            if (valueToIdentifier.Count == 0)
            {
                return;
            }

            var sb = new StringBuilder(4096);
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("namespace LiveOps.Keys.Generated");
            sb.AppendLine("{");
            sb.AppendLine("  public static class LiveOpsKeys");
            sb.AppendLine("  {");
            var sorted = new List<KeyValuePair<string, string>>(valueToIdentifier);
            sorted.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
            foreach (KeyValuePair<string, string> kv in sorted)
            {
                string escaped = kv.Key.Replace(@"\", @"\\").Replace("\"", "\\\"");
                sb.Append("    public const string ").Append(kv.Value).Append(" = \"").Append(escaped).AppendLine("\";");
            }

            sb.AppendLine("  }");
            sb.AppendLine("}");
            context.AddSource("LiveOpsKeys.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static void ReportWireKeyCollisionsIfAny(
            SourceProductionContext context,
            IReadOnlyList<KeyTypeRecord> keyRecords,
            INamedTypeSymbol? moduleRequest)
        {
            if (moduleRequest is null)
            {
                return;
            }

            var wireToTypes = new Dictionary<string, List<INamedTypeSymbol>>(StringComparer.Ordinal);
            foreach (KeyTypeRecord r in keyRecords)
            {
                INamedTypeSymbol t = r.Type;
                if (t.IsAbstract || t.TypeKind is not (TypeKind.Class or TypeKind.Struct))
                {
                    continue;
                }

                string? asmName = t.ContainingAssembly.Name;
                if (asmName is not null && asmName.IndexOf("Test", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                if (!r.IsModuleRequestSubtype)
                {
                    continue;
                }

                string wire = r.WireKey!;
                if (!wireToTypes.TryGetValue(wire, out List<INamedTypeSymbol>? list))
                {
                    list = new List<INamedTypeSymbol>(2);
                    wireToTypes[wire] = list;
                }

                list.Add(t);
            }

            foreach (KeyValuePair<string, List<INamedTypeSymbol>> kv in wireToTypes)
            {
                if (kv.Value.Count < 2)
                {
                    continue;
                }

                var names = new List<string>(kv.Value.Count);
                foreach (INamedTypeSymbol t in kv.Value)
                {
                    names.Add(t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                }

                string joined = string.Join(", ", names);
                context.ReportDiagnostic(
                    Diagnostic.Create(s_duplicateWire, Location.None, kv.Key, joined));
            }
        }

        private static void ReportMissingKeyMapInDtoAssembliesIfAny(
            SourceProductionContext context,
            Compilation compilation,
            IReadOnlyList<KeyTypeRecord> keyRecords)
        {
            if (!string.Equals(compilation.Assembly.Name, "LiveOps", StringComparison.Ordinal))
            {
                return;
            }

            var reported = new HashSet<IAssemblySymbol>(SymbolEqualityComparer.Default);
            foreach (KeyTypeRecord r in keyRecords)
            {
                if (!r.HasLiveOpsKeyAttribute)
                {
                    continue;
                }

                IAssemblySymbol asm = r.Type.ContainingAssembly;
                string? asmName = asm.Name;
                if (asmName is not null && asmName.IndexOf("Test", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                if (SymbolEqualityComparer.Default.Equals(asm, compilation.Assembly))
                {
                    continue;
                }

                if (!reported.Add(asm))
                {
                    continue;
                }

                if (FindLiveOpsKeyRuntimeMapType(asm) is not null)
                {
                    continue;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(s_missingGeneratorRef, Location.None, asmName ?? asm.ToDisplayString()));
            }
        }

        private static INamedTypeSymbol? FindLiveOpsKeyRuntimeMapType(IAssemblySymbol assembly)
        {
            INamespaceSymbol? ns = assembly.GlobalNamespace;
            ns = FindChildNamespace(ns, "LiveOps");
            if (ns is null)
            {
                return null;
            }

            ns = FindChildNamespace(ns, "Keys");
            if (ns is null)
            {
                return null;
            }

            ns = FindChildNamespace(ns, "Generated");
            if (ns is null)
            {
                return null;
            }

            foreach (INamedTypeSymbol t in ns.GetTypeMembers())
            {
                if (t.Name == "LiveOpsKeyRuntimeMap")
                {
                    return t;
                }
            }

            return null;
        }

        private static INamespaceSymbol? FindChildNamespace(INamespaceSymbol parent, string name)
        {
            foreach (INamespaceSymbol child in parent.GetNamespaceMembers())
            {
                if (child.Name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            INamedTypeSymbol? p = type;
            while (p is not null)
            {
                if (SymbolEqualityComparer.Default.Equals(p, baseType))
                {
                    return true;
                }

                p = p.BaseType;
            }

            return false;
        }

        private static void EmitRuntimeKeyMap(
            SourceProductionContext context,
            Compilation compilation,
            IReadOnlyList<KeyTypeRecord> keyRecords)
        {
            IAssemblySymbol sourceAssembly = compilation.Assembly;
            var list = new List<KeyTypeRecord>(32);
            foreach (KeyTypeRecord r in keyRecords)
            {
                if (!r.HasLiveOpsKeyAttribute)
                {
                    continue;
                }

                if (!SymbolEqualityComparer.Default.Equals(r.Type.ContainingAssembly, sourceAssembly))
                {
                    continue;
                }

                list.Add(r);
            }

            if (list.Count == 0)
            {
                return;
            }

            list.Sort(
                (a, b) => string.Compare(
                    a.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    b.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    StringComparison.Ordinal));

            var sb = new StringBuilder(8192);
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using LiveOps.DTO.Keys;");
            sb.AppendLine();
            sb.AppendLine("namespace LiveOps.Keys.Generated");
            sb.AppendLine("{");
            sb.AppendLine("  internal static class LiveOpsKeyRuntimeMap");
            sb.AppendLine("  {");
            sb.AppendLine("    private static readonly KeyValuePair<RuntimeTypeHandle, global::LiveOps.DTO.Keys.LiveOpsKeyResolution>[] s_entries =");
            sb.AppendLine("      new KeyValuePair<RuntimeTypeHandle, global::LiveOps.DTO.Keys.LiveOpsKeyResolution>[]");
            sb.AppendLine("      {");

            for (int i = 0; i < list.Count; i++)
            {
                KeyTypeRecord e = list[i];
                string typeFq = e.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string mEsc = EscapeCSharpStringLiteral(e.ModuleKey);
                sb.Append("        new KeyValuePair<RuntimeTypeHandle, global::LiveOps.DTO.Keys.LiveOpsKeyResolution>(")
                    .Append("typeof(")
                    .Append(typeFq)
                    .Append(").TypeHandle, new global::LiveOps.DTO.Keys.LiveOpsKeyResolution(\"")
                    .Append(mEsc)
                    .Append("\", ");
                if (e.WireKey is null)
                {
                    sb.Append("null");
                }
                else
                {
                    sb.Append('"').Append(EscapeCSharpStringLiteral(e.WireKey)).Append('"');
                }

                sb.Append("))");
                sb.AppendLine(i < list.Count - 1 ? "," : string.Empty);
            }

            sb.AppendLine("      };");
            sb.AppendLine();
            sb.AppendLine("    [ModuleInitializer]");
            sb.AppendLine("    internal static void Register()");
            sb.AppendLine("    {");
            sb.AppendLine("      global::LiveOps.DTO.Keys.LiveOpsKeyResolver.Contribute(s_entries);");
            sb.AppendLine("    }");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("namespace System.Runtime.CompilerServices");
            sb.AppendLine("{");
            sb.AppendLine("  [AttributeUsage(AttributeTargets.Method, Inherited = false)]");
            sb.AppendLine("  internal sealed class ModuleInitializerAttribute : Attribute");
            sb.AppendLine("  {");
            sb.AppendLine("  }");
            sb.AppendLine("}");

            context.AddSource("LiveOpsKeyRuntimeMap.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static string EscapeCSharpStringLiteral(string s)
        {
            if (s is null)
            {
                return string.Empty;
            }

            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string MakeUniqueConstIdentifier(string value, HashSet<string> usedConstNames)
        {
            if (string.IsNullOrEmpty(value))
            {
                return TakeUnique("K_Empty", usedConstNames);
            }

            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i == 0)
                {
                    if (char.IsLetter(c) || c == '_')
                    {
                        sb.Append(c);
                    }
                    else if (char.IsDigit(c))
                    {
                        sb.Append('K').Append(c);
                    }
                    else
                    {
                        sb.Append('_');
                    }
                }
                else
                {
                    if (char.IsLetterOrDigit(c) || c == '_')
                    {
                        sb.Append(c);
                    }
                    else
                    {
                        sb.Append('_');
                    }
                }
            }

            if (sb.Length == 0)
            {
                sb.Append("K_Value");
            }

            string baseName = sb.ToString();
            if (CSharpKeywords.Contains(baseName))
            {
                baseName = "K_" + baseName;
            }

            return TakeUnique(baseName, usedConstNames);
        }

        private static string TakeUnique(string baseName, HashSet<string> usedConstNames)
        {
            if (usedConstNames.Add(baseName))
            {
                return baseName;
            }

            for (int n = 1; n < 10_000; n++)
            {
                string alt = baseName + "_" + n;
                if (usedConstNames.Add(alt))
                {
                    return alt;
                }
            }

            return baseName + "_dup";
        }

        private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
        };

        private static bool IsRelevantRef(IAssemblySymbol a)
        {
            string? n = a.Name;
            if (string.IsNullOrEmpty(n) || n.IndexOf("Test", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            if (!n.StartsWith("LiveOps", StringComparison.Ordinal))
            {
                return false;
            }

            return n is "LiveOps.Modules" or "LiveOps.Core" or "LiveOps" or "LiveOps.DTO" or "LiveOps.Modules.DTO"
                || (n.EndsWith(".DTO", StringComparison.Ordinal) && n.StartsWith("LiveOps.", StringComparison.Ordinal));
        }

        private static void VisitAssembly(IAssemblySymbol assembly, HashSet<INamedTypeSymbol> outTypes)
        {
            Visit(assembly.GlobalNamespace, outTypes);
        }

        private static void Visit(INamespaceSymbol ns, HashSet<INamedTypeSymbol> outTypes)
        {
            foreach (INamespaceSymbol child in ns.GetNamespaceMembers())
            {
                Visit(child, outTypes);
            }

            foreach (INamedTypeSymbol t in ns.GetTypeMembers())
            {
                AddNamed(t, outTypes);
            }
        }

        private static void AddNamed(INamedTypeSymbol t, HashSet<INamedTypeSymbol> outTypes)
        {
            if (t.IsStatic)
            {
                return;
            }

            if (t.TypeKind is TypeKind.Class or TypeKind.Struct)
            {
                outTypes.Add(t);
            }

            foreach (INamedTypeSymbol nested in t.GetTypeMembers())
            {
                AddNamed(nested, outTypes);
            }
        }

        private static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol iface)
        {
            foreach (INamedTypeSymbol? i in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(i, iface) ||
                    SymbolEqualityComparer.Default.Equals(i.ConstructedFrom, iface) ||
                    SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iface.OriginalDefinition))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetHandlerArgs(
            INamedTypeSymbol type,
            INamedTypeSymbol iGameHandler2Def,
            out ITypeSymbol? requestType,
            out ITypeSymbol? responseType)
        {
            requestType = null;
            responseType = null;
            foreach (INamedTypeSymbol? i in type.AllInterfaces)
            {
                if (i.IsGenericType &&
                    SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iGameHandler2Def) &&
                    i.TypeArguments.Length == 2)
                {
                    requestType = i.TypeArguments[0];
                    responseType = i.TypeArguments[1];
                    return true;
                }
            }

            return false;
        }

        private static void EmitEmpty(SourceProductionContext context)
        {
            Emit(context, Array.Empty<Entry>());
        }

        private static void Emit(SourceProductionContext context, IReadOnlyList<Entry> entries)
        {
            var sb = new StringBuilder(4096);
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("namespace LiveOps.Initialize");
            sb.AppendLine("{");
            sb.AppendLine("  internal static partial class LiveOpsManifest");
            sb.AppendLine("  {");
            sb.AppendLine("    private static readonly global::LiveOps.Initialize.LiveOpsManifestEntry[] _entries = new global::LiveOps.Initialize.LiveOpsManifestEntry[]");
            sb.AppendLine("    {");

            foreach (Entry e in entries)
            {
                string full = e.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (e.IsHandler && e.Req is not null && e.Res is not null)
                {
                    string reqFull = e.Req.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    string resFull = e.Res.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    sb.Append("      new global::LiveOps.Initialize.LiveOpsManifestEntry(typeof(")
                        .Append(full)
                        .Append("), isGameApiHandler: true, isGameModule: ")
                        .Append(e.IsModule ? "true" : "false")
                        .Append(", requestType: typeof(")
                        .Append(reqFull)
                        .Append("), responseType: typeof(")
                        .Append(resFull)
                        .AppendLine(")),");
                }
                else
                {
                    sb.Append("      new global::LiveOps.Initialize.LiveOpsManifestEntry(typeof(")
                        .Append(full)
                        .Append("), isGameApiHandler: ")
                        .Append(e.IsHandler ? "true" : "false")
                        .Append(", isGameModule: ")
                        .Append(e.IsModule ? "true" : "false")
                        .AppendLine("),");
                }
            }

            sb.AppendLine("    };");
            sb.AppendLine("    public static global::System.ReadOnlySpan<global::LiveOps.Initialize.LiveOpsManifestEntry> Entries => _entries;");
            sb.AppendLine("  }");
            sb.AppendLine("}");

            context.AddSource("LiveOpsManifest.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private readonly struct KeyTypeRecord
        {
            public KeyTypeRecord(
                INamedTypeSymbol type,
                string moduleKey,
                string? wireKey,
                bool hasLiveOpsKeyAttribute,
                bool isModuleRequestSubtype,
                bool isGameModuleData,
                bool isGameApiHandler,
                bool isGameModule,
                ITypeSymbol? requestType,
                ITypeSymbol? responseType)
            {
                Type = type;
                ModuleKey = moduleKey;
                WireKey = wireKey;
                HasLiveOpsKeyAttribute = hasLiveOpsKeyAttribute;
                IsModuleRequestSubtype = isModuleRequestSubtype;
                IsGameModuleData = isGameModuleData;
                IsGameApiHandler = isGameApiHandler;
                IsGameModule = isGameModule;
                RequestType = requestType;
                ResponseType = responseType;
            }

            public INamedTypeSymbol Type { get; }

            public string ModuleKey { get; }

            public string? WireKey { get; }

            public bool HasLiveOpsKeyAttribute { get; }

            public bool IsModuleRequestSubtype { get; }

            public bool IsGameModuleData { get; }

            public bool IsGameApiHandler { get; }

            public bool IsGameModule { get; }

            public ITypeSymbol? RequestType { get; }

            public ITypeSymbol? ResponseType { get; }
        }

        private readonly struct Entry
        {
            public Entry(INamedTypeSymbol type, bool isHandler, bool isModule, ITypeSymbol? req, ITypeSymbol? res)
            {
                Type = type;
                IsHandler = isHandler;
                IsModule = isModule;
                Req = req;
                Res = res;
            }

            public INamedTypeSymbol Type { get; }
            public bool IsHandler { get; }
            public bool IsModule { get; }
            public ITypeSymbol? Req { get; }
            public ITypeSymbol? Res { get; }
        }
    }
}
