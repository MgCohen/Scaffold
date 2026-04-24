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
            if (iGameHandler2 is null && iModule is null)
            {
                EmitEmpty(context);
                return;
            }

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

            var list = new List<Entry>(types.Count);
            foreach (INamedTypeSymbol t in types)
            {
                if (t.IsAbstract)
                {
                    continue;
                }

                if (t.TypeKind is not (TypeKind.Class or TypeKind.Struct))
                {
                    continue;
                }

                ITypeSymbol? req = null;
                ITypeSymbol? res = null;
                bool h = iGameHandler2 is not null && TryGetHandlerArgs(t, iGameHandler2, out req, out res);
                bool m = iModule is not null && ImplementsInterface(t, iModule);
                if (h || m)
                {
                    list.Add(new Entry(t, h, m, h ? req : null, h ? res : null));
                }
            }

            Emit(context, list);
        }

        private static bool IsRelevantRef(IAssemblySymbol a)
        {
            string? n = a.Name;
            if (string.IsNullOrEmpty(n) || n.IndexOf("Test", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return n.StartsWith("LiveOps", StringComparison.Ordinal) &&
                (n is "LiveOps.Modules" or "LiveOps.Core" or "LiveOps" or "LiveOps.DTO" or "LiveOps.Modules.DTO");
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
