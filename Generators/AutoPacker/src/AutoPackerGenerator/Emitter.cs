using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Scaffold.AutoPacker;

namespace AutoPackerGenerator
{
    internal static class Emitter
    {
        public static string EmitSource(
            INamedTypeSymbol type,
            IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> ownFields,
            IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> inheritedFields,
            IReadOnlyList<IMethodSymbol> extensionMethods,
            bool ancestorEmits,
            bool descendantEmits,
            bool typedAncestorOverride,
            bool typedAncestorHides,
            bool isAbstractMarker)
        {
            bool hasNamespace = !type.ContainingNamespace.IsGlobalNamespace;
            var allFields = Combine(inheritedFields, ownFields);
            var sb = new StringBuilder();

            AppendUsings(sb, allFields);

            if (hasNamespace)
                AppendNamespaceOpen(sb, type);

            AppendTypeOpen(sb, type, hasNamespace, isAbstractMarker);

            if (isAbstractMarker)
            {
                AppendAbstractMarkerMembers(sb, hasNamespace);
            }
            else
            {
                AppendDefaultConstructorIfMissing(sb, type, hasNamespace);
                AppendConstructorFromPacked(sb, type, allFields, hasNamespace, extensionMethods, ancestorEmits, descendantEmits, typedAncestorOverride, typedAncestorHides);
                AppendPackMethod(sb, hasNamespace, ancestorEmits, descendantEmits, typedAncestorOverride, typedAncestorHides);
                AppendPackedStruct(sb, type, allFields, hasNamespace, extensionMethods);
            }

            AppendTypeClose(sb, hasNamespace);

            if (hasNamespace)
                AppendNamespaceClose(sb);

            return sb.ToString();
        }

        private static void AppendAbstractMarkerMembers(StringBuilder sb, bool indented)
        {
            string i1 = indented ? "        " : "    ";
            sb.AppendLine($"{i1}public abstract {nameof(IPackedStruct)} Pack({nameof(IPackingHandler)} handler = null);");
            sb.AppendLine();
            sb.AppendLine($"{i1}public abstract void Unpack({nameof(IPackedStruct)} packed, {nameof(IPackingHandler)} handler = null);");
            sb.AppendLine();
        }

        private static IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> Combine(
            IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> inherited,
            IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> own)
        {
            var combined = new List<(IFieldSymbol, ITypeSymbol)>(inherited.Count + own.Count);
            combined.AddRange(inherited);
            combined.AddRange(own);
            return combined;
        }

        private static string MethodModifier(bool ancestorEmits, bool descendantEmits)
        {
            if (ancestorEmits) return "override ";
            if (descendantEmits) return "virtual ";
            return string.Empty;
        }

        // Typed methods aren't virtual by default. `override` only when a hand-written base
        // declares abstract/virtual typed methods (e.g. WireEvent<TPacked>). `new` only when
        // an emitted ancestor with fields would silently hide on inheritance and produce CS0108.
        private static string TypedMethodModifier(bool typedAncestorOverride, bool typedAncestorHides)
        {
            if (typedAncestorOverride) return "override ";
            if (typedAncestorHides) return "new ";
            return string.Empty;
        }

        private static void AppendUsings(StringBuilder sb, IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> fields)
        {
            sb.AppendLine("using System;");
            sb.AppendLine("using Scaffold.AutoPacker;");
            foreach (var ns in CollectConversionNamespaces(fields))
                sb.AppendLine($"using {ns};");
            sb.AppendLine();
        }

        private static IEnumerable<string> CollectConversionNamespaces(IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> fields)
        {
            var seen = new HashSet<string>();
            foreach (var tuple in fields)
            {
                if (!TypeConversions.TryGetConversion(tuple.Field.Type, out var targetFull))
                    continue;
                var ns = TypeConversions.GetNamespace(targetFull);
                if (ns != null && seen.Add(ns))
                    yield return ns;
            }
        }

        private static void AppendNamespaceOpen(StringBuilder sb, INamedTypeSymbol type)
        {
            sb.AppendLine($"namespace {type.ContainingNamespace.ToDisplayString()}");
            sb.AppendLine("{");
        }

        private static void AppendNamespaceClose(StringBuilder sb)
        {
            sb.AppendLine("}");
        }

        private static void AppendTypeOpen(StringBuilder sb, INamedTypeSymbol type, bool indented, bool isAbstractMarker)
        {
            string indent = indented ? "    " : "";
            string keyword = GetTypeKeyword(type);
            string accessibility = GetAccessibilityKeyword(type.DeclaredAccessibility);
            // IUnpackable is class-only: structs box on interface dispatch and Unpack would mutate the box, not the original.
            // Typed IPackable<Packed>/IUnpackable<Packed> are only emitted when there's a Packed struct to point at —
            // abstract markers (no fields, no inherited fields) have none.
            bool isStruct = type.TypeKind == TypeKind.Struct;
            string interfaces;
            if (isAbstractMarker)
            {
                interfaces = isStruct
                    ? nameof(IPackable)
                    : $"{nameof(IPackable)}, {nameof(IUnpackable)}";
            }
            else
            {
                // At type-declaration position, the nested `Packed` isn't in scope yet — qualify
                // it via the containing self-reference (e.g. `Foo<T>.Packed`) so the binder resolves it.
                string packedRef = $"{FormatSelfTypeReference(type)}.Packed";
                interfaces = isStruct
                    ? $"{nameof(IPackable)}, IPackable<{packedRef}>"
                    : $"{nameof(IPackable)}, {nameof(IUnpackable)}, IPackable<{packedRef}>, IUnpackable<{packedRef}>";
            }
            string typeParams = FormatTypeParameterList(type);
            sb.AppendLine($"{indent}{accessibility} partial {keyword} {type.Name}{typeParams} : {interfaces}");
            sb.AppendLine($"{indent}{{");
        }

        private static string FormatTypeParameterList(INamedTypeSymbol type)
        {
            if (type.TypeParameters.Length == 0) return string.Empty;
            var names = new List<string>(type.TypeParameters.Length);
            foreach (var tp in type.TypeParameters) names.Add(tp.Name);
            return "<" + string.Join(", ", names) + ">";
        }

        // Self-references inside the body (typeof(...), `Packed(SourceType source)` ctor param).
        // Constructors use the bare name; type-references must include type params.
        private static string FormatSelfTypeReference(INamedTypeSymbol type) =>
            type.Name + FormatTypeParameterList(type);

        private static void AppendTypeClose(StringBuilder sb, bool indented)
        {
            string indent = indented ? "    " : "";
            sb.AppendLine($"{indent}}}");
        }

        private static void AppendDefaultConstructorIfMissing(StringBuilder sb, INamedTypeSymbol type, bool indented)
        {
            if (type.TypeKind == TypeKind.Struct)
                return;

            bool hasExplicitParameterlessConstructor = false;
            foreach (var constructor in type.Constructors)
            {
                if (constructor.Parameters.Length == 0 && !constructor.IsImplicitlyDeclared)
                {
                    hasExplicitParameterlessConstructor = true;
                    break;
                }
            }

            if (!hasExplicitParameterlessConstructor)
            {
                string indent = indented ? "        " : "    ";
                sb.AppendLine($"{indent}public {type.Name}() {{ }}");
                sb.AppendLine();
            }
        }

        private static void AppendConstructorFromPacked(
            StringBuilder sb,
            INamedTypeSymbol type,
            IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> fields,
            bool indented,
            IReadOnlyList<IMethodSymbol> extensionMethods,
            bool ancestorEmits,
            bool descendantEmits,
            bool typedAncestorOverride,
            bool typedAncestorHides)
        {
            string i1 = indented ? "        " : "    ";
            string i2 = indented ? "            " : "        ";
            string typedModifier = TypedMethodModifier(typedAncestorOverride, typedAncestorHides);

            if (type.TypeKind == TypeKind.Struct)
            {
                // Struct path: ctor + typed UnpackTyped body, no IUnpackable.
                // Convention: the typed surface owns the body; the ctor delegates so there's
                // a single field-assignment site.
                sb.AppendLine($"{i1}public {type.Name}(Packed packedData, {nameof(IPackingHandler)} handler = null)");
                sb.AppendLine($"{i1}{{");
                sb.AppendLine($"{i2}handler ??= new DefaultPackingHandler();");
                foreach (var tuple in fields)
                    AppendFieldAssignment(sb, tuple.Field, tuple.TargetType, "packedData", "handler", convertToPacked: false, i2, extensionMethods);
                sb.AppendLine($"{i1}}}");
                sb.AppendLine();
                return;
            }

            // Class path: typed UnpackTyped(in Packed, …) owns the body. The ctor and the
            // boxed Unpack(IPackedStruct, …) both delegate to it — one source of truth, and
            // the boxed path is the only place where an unbox cast happens.
            sb.AppendLine($"{i1}public {type.Name}(Packed packedData, {nameof(IPackingHandler)} handler = null)");
            sb.AppendLine($"{i1}    => UnpackTyped(packedData, handler);");
            sb.AppendLine();

            string boxedModifier = MethodModifier(ancestorEmits, descendantEmits);
            sb.AppendLine($"{i1}public {boxedModifier}void Unpack({nameof(IPackedStruct)} packed, {nameof(IPackingHandler)} handler = null)");
            sb.AppendLine($"{i1}    => UnpackTyped((Packed)packed, handler);");
            sb.AppendLine();

            sb.AppendLine($"{i1}public {typedModifier}void UnpackTyped(in Packed packedData, {nameof(IPackingHandler)} handler = null)");
            sb.AppendLine($"{i1}{{");
            sb.AppendLine($"{i2}handler ??= new DefaultPackingHandler();");
            foreach (var tuple in fields)
                AppendFieldAssignment(sb, tuple.Field, tuple.TargetType, "packedData", "handler", convertToPacked: false, i2, extensionMethods);
            sb.AppendLine($"{i1}}}");
            sb.AppendLine();
        }

        private static void AppendPackMethod(StringBuilder sb, bool indented, bool ancestorEmits, bool descendantEmits, bool typedAncestorOverride, bool typedAncestorHides)
        {
            string i1 = indented ? "        " : "    ";
            string boxedModifier = MethodModifier(ancestorEmits, descendantEmits);
            string typedModifier = TypedMethodModifier(typedAncestorOverride, typedAncestorHides);

            // Typed Pack owns the body. Boxed Pack returns the same value through IPackedStruct
            // (the struct boxes at the return-type boundary — that's the only allocation on this path).
            sb.AppendLine($"{i1}public {typedModifier}Packed PackTyped({nameof(IPackingHandler)} handler = null)");
            sb.AppendLine($"{i1}{{");
            sb.AppendLine($"{i1}    return new Packed(this, handler);");
            sb.AppendLine($"{i1}}}");
            sb.AppendLine();

            sb.AppendLine($"{i1}public {boxedModifier}{nameof(IPackedStruct)} Pack({nameof(IPackingHandler)} handler = null)");
            sb.AppendLine($"{i1}    => PackTyped(handler);");
            sb.AppendLine();
        }

        private static void AppendPackedStruct(StringBuilder sb, INamedTypeSymbol type, IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> fields, bool indented, IReadOnlyList<IMethodSymbol> extensionMethods)
        {
            string i1 = indented ? "        " : "    ";
            string i2 = indented ? "            " : "        ";
            string i3 = indented ? "                " : "            ";

            sb.AppendLine($"{i1}public struct Packed : {nameof(IPackedStruct)}");
            sb.AppendLine($"{i1}{{");
            sb.AppendLine($"{i2}public Type PackedType => typeof({FormatSelfTypeReference(type)});");
            sb.AppendLine();

            AppendPackedConstructor(sb, type, fields, i2, i3, extensionMethods);
            AppendPackedFields(sb, fields, i2);

            sb.AppendLine($"{i1}}}");
        }

        private static void AppendPackedConstructor(StringBuilder sb, INamedTypeSymbol type, IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> fields, string i2, string i3, IReadOnlyList<IMethodSymbol> extensionMethods)
        {
            sb.AppendLine($"{i2}public Packed({FormatSelfTypeReference(type)} source, {nameof(IPackingHandler)} handler = null)");
            sb.AppendLine($"{i2}{{");
            sb.AppendLine($"{i3}handler ??= new DefaultPackingHandler();");
            foreach (var tuple in fields)
                AppendFieldAssignment(sb, tuple.Field, tuple.TargetType, "source", "handler", convertToPacked: true, i3, extensionMethods);
            sb.AppendLine($"{i2}}}");
            sb.AppendLine();
        }

        private static void AppendPackedFields(StringBuilder sb, IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> fields, string i2)
        {
            foreach (var tuple in fields)
            {
                var field = tuple.Field;
                var targetType = tuple.TargetType;

                string typeName;
                if (targetType != null)
                {
                    typeName = targetType.ToDisplayString();
                }
                else if (TypeConversions.TryGetConversion(field.Type, out var targetFull))
                {
                    typeName = TypeConversions.GetShortName(targetFull);
                }
                else
                {
                    typeName = field.Type.ToDisplayString();
                }

                sb.AppendLine($"{i2}public {typeName} {field.Name};");
            }
        }

        private static void AppendFieldAssignment(
            StringBuilder sb,
            IFieldSymbol field,
            ITypeSymbol targetType,
            string sourceName,
            string handlerName,
            bool convertToPacked,
            string indent,
            IReadOnlyList<IMethodSymbol> extensionMethods)
        {
            var sourceTypeName = field.Type.ToDisplayString();
            string targetTypeName;
            if (targetType != null)
            {
                targetTypeName = targetType.ToDisplayString();
            }
            else if (TypeConversions.TryGetConversion(field.Type, out var targetFull))
            {
                targetTypeName = targetFull;
            }
            else
            {
                targetTypeName = sourceTypeName;
            }

            var fromType = convertToPacked ? sourceTypeName : targetTypeName;
            var toType = convertToPacked ? targetTypeName : sourceTypeName;

            if (HasMatchingExtensionMethod(extensionMethods, fromType, toType))
            {
                sb.AppendLine($"{indent}{field.Name} = {handlerName}.Resolve({sourceName}.{field.Name});");
            }
            else
            {
                sb.AppendLine($"{indent}{field.Name} = {handlerName}.Resolve<{fromType}, {toType}>({sourceName}.{field.Name});");
            }
        }

        private static bool HasMatchingExtensionMethod(IReadOnlyList<IMethodSymbol> extensionMethods, string sourceTypeName, string targetTypeName)
        {
            if (extensionMethods == null) return false;
            foreach (var method in extensionMethods)
            {
                if (method.Parameters.Length >= 2)
                {
                    var sourceType = method.Parameters[1].Type.ToDisplayString();
                    var returnType = method.ReturnType.ToDisplayString();
                    if (sourceType == sourceTypeName && returnType == targetTypeName)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // ---- Registry emission ----

        public static string EmitRegistry(IReadOnlyList<INamedTypeSymbol> types)
        {
            var sb = new StringBuilder();
            AppendRegistryUsings(sb);
            AppendRegistryClassOpen(sb);
            AppendRegistryField(sb);
            AppendRegistryStaticConstructor(sb, types);
            AppendRegisterMethod(sb);
            AppendGetPackableTypesMethod(sb);
            AppendRegistryClassClose(sb);
            return sb.ToString();
        }

        private static void AppendRegistryUsings(StringBuilder sb)
        {
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
        }

        private static void AppendRegistryClassOpen(StringBuilder sb)
        {
            sb.AppendLine("public static class AutoPackerRegistry");
            sb.AppendLine("{");
        }

        private static void AppendRegistryClassClose(StringBuilder sb)
        {
            sb.AppendLine("}");
        }

        private static void AppendRegistryField(StringBuilder sb)
        {
            sb.AppendLine("    private static readonly List<Dictionary<Type, Type>> _types;");
            sb.AppendLine();
        }

        private static void AppendRegistryStaticConstructor(StringBuilder sb, IReadOnlyList<INamedTypeSymbol> types)
        {
            sb.AppendLine("    static AutoPackerRegistry()");
            sb.AppendLine("    {");
            sb.AppendLine("        _types = new List<Dictionary<Type, Type>>();");
            foreach (var type in types)
                AppendRegisterCall(sb, type);
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private static void AppendRegisterCall(StringBuilder sb, INamedTypeSymbol type)
        {
            var fqn = type.ToDisplayString();
            sb.AppendLine($"        Register(typeof({fqn}), typeof({fqn}.Packed));");
        }

        private static void AppendRegisterMethod(StringBuilder sb)
        {
            sb.AppendLine("    private static void Register(Type sourceType, Type packedType)");
            sb.AppendLine("    {");
            sb.AppendLine("        _types.Add(new Dictionary<Type, Type> { { sourceType, packedType } });");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private static void AppendGetPackableTypesMethod(StringBuilder sb)
        {
            sb.AppendLine("    public static List<Dictionary<Type, Type>> GetPackableTypes() => _types;");
        }

        // ---- Type keyword / accessibility helpers ----

        private static string GetTypeKeyword(INamedTypeSymbol type)
        {
            if (type.IsRecord)
                return type.TypeKind == TypeKind.Struct ? "record struct" : "record";
            return type.TypeKind == TypeKind.Struct ? "struct" : "class";
        }

        private static string GetAccessibilityKeyword(Accessibility accessibility)
        {
            switch (accessibility)
            {
                case Accessibility.Public: return "public";
                case Accessibility.Internal: return "internal";
                case Accessibility.Protected: return "protected";
                case Accessibility.ProtectedOrInternal: return "protected internal";
                case Accessibility.ProtectedAndInternal: return "private protected";
                default: return "public";
            }
        }
    }
}
