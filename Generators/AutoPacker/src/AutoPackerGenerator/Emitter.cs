using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Scaffold.AutoPacker;

namespace AutoPackerGenerator
{
    internal static class Emitter
    {
        public static string EmitSource(INamedTypeSymbol type, IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> fields, IReadOnlyList<IMethodSymbol> extensionMethods)
        {
            bool hasNamespace = !type.ContainingNamespace.IsGlobalNamespace;
            var sb = new StringBuilder();

            AppendUsings(sb, fields);

            if (hasNamespace)
                AppendNamespaceOpen(sb, type);

            AppendTypeOpen(sb, type, hasNamespace);
            AppendDefaultConstructorIfMissing(sb, type, hasNamespace);
            AppendConstructorFromPacked(sb, type, fields, hasNamespace, extensionMethods);
            AppendPackMethod(sb, hasNamespace);
            AppendPackedStruct(sb, type, fields, hasNamespace, extensionMethods);
            AppendTypeClose(sb, hasNamespace);

            if (hasNamespace)
                AppendNamespaceClose(sb);

            return sb.ToString();
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

        private static void AppendTypeOpen(StringBuilder sb, INamedTypeSymbol type, bool indented)
        {
            string indent = indented ? "    " : "";
            string keyword = GetTypeKeyword(type);
            string accessibility = GetAccessibilityKeyword(type.DeclaredAccessibility);
            sb.AppendLine($"{indent}{accessibility} partial {keyword} {type.Name} : {nameof(IPackable)}");
            sb.AppendLine($"{indent}{{");
        }

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

        private static void AppendConstructorFromPacked(StringBuilder sb, INamedTypeSymbol type, IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> fields, bool indented, IReadOnlyList<IMethodSymbol> extensionMethods)
        {
            string i1 = indented ? "        " : "    ";
            string i2 = indented ? "            " : "        ";

            sb.AppendLine($"{i1}public {type.Name}({type.Name}.Packed packedData, {nameof(IPackingHandler)} handler = null)");
            sb.AppendLine($"{i1}{{");
            sb.AppendLine($"{i2}handler ??= new DefaultPackingHandler();");
            foreach (var tuple in fields)
                AppendFieldAssignment(sb, tuple.Field, tuple.TargetType, "packedData", "handler", convertToPacked: false, i2, extensionMethods);
            sb.AppendLine($"{i1}}}");
            sb.AppendLine();
        }

        private static void AppendPackMethod(StringBuilder sb, bool indented)
        {
            string i1 = indented ? "        " : "    ";
            sb.AppendLine($"{i1}public {nameof(IPackedStruct)} Pack({nameof(IPackingHandler)} handler = null)");
            sb.AppendLine($"{i1}{{");
            sb.AppendLine($"{i1}    return new Packed(this, handler);");
            sb.AppendLine($"{i1}}}");
            sb.AppendLine();
        }

        private static void AppendPackedStruct(StringBuilder sb, INamedTypeSymbol type, IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> fields, bool indented, IReadOnlyList<IMethodSymbol> extensionMethods)
        {
            string i1 = indented ? "        " : "    ";
            string i2 = indented ? "            " : "        ";
            string i3 = indented ? "                " : "            ";

            sb.AppendLine($"{i1}public struct Packed : {nameof(IPackedStruct)}");
            sb.AppendLine($"{i1}{{");
            sb.AppendLine($"{i2}public Type PackedType => typeof({type.Name});");
            sb.AppendLine();

            AppendPackedConstructor(sb, type, fields, i2, i3, extensionMethods);
            AppendPackedFields(sb, fields, i2);

            sb.AppendLine($"{i1}}}");
        }

        private static void AppendPackedConstructor(StringBuilder sb, INamedTypeSymbol type, IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> fields, string i2, string i3, IReadOnlyList<IMethodSymbol> extensionMethods)
        {
            sb.AppendLine($"{i2}public Packed({type.Name} source, {nameof(IPackingHandler)} handler = null)");
            sb.AppendLine($"{i2}{{");
            sb.AppendLine($"{i3}handler ??= new DefaultPackingHandler();");
            foreach (var tuple in fields)
                AppendFieldAssignment(sb, tuple.Field, tuple.TargetType, "source", "handler", convertToPacked: true, i3, extensionMethods);;
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
