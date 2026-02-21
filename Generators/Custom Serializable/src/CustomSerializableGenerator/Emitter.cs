using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace CustomSerializableGenerator
{
    internal static class Emitter
    {
        public static string EmitSource(INamedTypeSymbol type, IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> fields)
        {
            bool hasNamespace = !type.ContainingNamespace.IsGlobalNamespace;
            var sb = new StringBuilder();

            AppendUsings(sb, fields);

            if (hasNamespace)
                AppendNamespaceOpen(sb, type);

            AppendTypeOpen(sb, type, hasNamespace);
            AppendConstructorFromSerializable(sb, type, fields, hasNamespace);
            AppendSerializeMethod(sb, hasNamespace);
            AppendSerializableStruct(sb, type, fields, hasNamespace);
            AppendTypeClose(sb, hasNamespace);

            if (hasNamespace)
                AppendNamespaceClose(sb);

            return sb.ToString();
        }

        private static void AppendUsings(StringBuilder sb, IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> fields)
        {
            sb.AppendLine("using System;");
            sb.AppendLine("using Unity.Netcode;");
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
            sb.AppendLine($"{indent}{accessibility} partial {keyword} {type.Name} : {nameof(ISerializableSource)}");
            sb.AppendLine($"{indent}{{");
        }

        private static void AppendTypeClose(StringBuilder sb, bool indented)
        {
            string indent = indented ? "    " : "";
            sb.AppendLine($"{indent}}}");
        }

        private static void AppendConstructorFromSerializable(StringBuilder sb, INamedTypeSymbol type, IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> fields, bool indented)
        {
            string i1 = indented ? "        " : "    ";
            string i2 = indented ? "            " : "        ";

            sb.AppendLine($"{i1}public {type.Name}({type.Name}.Serializable serializable, {nameof(ISerializationResolver)} resolver = null)");
            sb.AppendLine($"{i1}{{");
            sb.AppendLine($"{i2}resolver ??= new DefaultSerializationResolver();");
            foreach (var tuple in fields)
                AppendFieldAssignment(sb, tuple.Field, tuple.TargetType, "serializable", "resolver", convertToSerialized: false, i2);
            sb.AppendLine($"{i1}}}");
            sb.AppendLine();
        }

        private static void AppendSerializeMethod(StringBuilder sb, bool indented)
        {
            string i1 = indented ? "        " : "    ";
            sb.AppendLine($"{i1}public {nameof(ISerializedStruct)} Serialize({nameof(ISerializationResolver)} resolver = null)");
            sb.AppendLine($"{i1}{{");
            sb.AppendLine($"{i1}    return new Serializable(this, resolver);");
            sb.AppendLine($"{i1}}}");
            sb.AppendLine();
        }

        private static void AppendSerializableStruct(StringBuilder sb, INamedTypeSymbol type, IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> fields, bool indented)
        {
            string i1 = indented ? "        " : "    ";
            string i2 = indented ? "            " : "        ";
            string i3 = indented ? "                " : "            ";

            sb.AppendLine($"{i1}public struct Serializable : {nameof(ISerializedStruct)}");
            sb.AppendLine($"{i1}{{");
            sb.AppendLine($"{i2}public Type SerializableType => typeof({type.Name});");
            sb.AppendLine();

            AppendSerializableConstructor(sb, type, fields, i2, i3);
            AppendSerializableFields(sb, fields, i2);

            sb.AppendLine($"{i1}}}");
        }

        private static void AppendSerializableConstructor(StringBuilder sb, INamedTypeSymbol type, IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> fields, string i2, string i3)
        {
            sb.AppendLine($"{i2}public Serializable({type.Name} source, {nameof(ISerializationResolver)} resolver = null)");
            sb.AppendLine($"{i2}{{");
            sb.AppendLine($"{i3}resolver ??= new DefaultSerializationResolver();");
            foreach (var tuple in fields)
                AppendFieldAssignment(sb, tuple.Field, tuple.TargetType, "source", "resolver", convertToSerialized: true, i3);;
            sb.AppendLine($"{i2}}}");
            sb.AppendLine();
        }

        private static void AppendSerializableFields(StringBuilder sb, IReadOnlyList<(IFieldSymbol Field, ITypeSymbol TargetType)> fields, string i2)
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
            string resolverName,
            bool convertToSerialized,
            string indent)
        {
            if (targetType != null)
            {
                var sourceTypeName = field.Type.ToDisplayString();
                var targetTypeName = targetType.ToDisplayString();
                var fromType = convertToSerialized ? sourceTypeName : targetTypeName;
                var toType = convertToSerialized ? targetTypeName : sourceTypeName;
                sb.AppendLine($"{indent}{field.Name} = {resolverName}.Resolve<{fromType}, {toType}>({sourceName}.{field.Name});");
            }
            else if (TypeConversions.TryGetConversion(field.Type, out var targetFull))
            {
                AppendConvertedAssignment(sb, field, sourceName, convertToSerialized, targetFull, indent);
            }
            else
            {
                sb.AppendLine($"{indent}{field.Name} = {sourceName}.{field.Name};");
            }
        }

        private static void AppendConvertedAssignment(
            StringBuilder sb,
            IFieldSymbol field,
            string sourceName,
            bool convertToSerialized,
            string targetFull,
            string indent)
        {
            var sourceTypeName = field.Type.ToDisplayString();
            var targetShortName = TypeConversions.GetShortName(targetFull);
            var fromType = convertToSerialized ? sourceTypeName : targetShortName;
            var toType = convertToSerialized ? targetShortName : sourceTypeName;
            sb.AppendLine($"{indent}{field.Name} = {sourceName}.{field.Name}.ConvertTo<{fromType}, {toType}>();");
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
            AppendGetSerializableTypesMethod(sb);
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
            sb.AppendLine("public static class SerializableTypeRegistry");
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
            sb.AppendLine("    static SerializableTypeRegistry()");
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
            sb.AppendLine($"        Register(typeof({fqn}), typeof({fqn}.Serializable));");
        }

        private static void AppendRegisterMethod(StringBuilder sb)
        {
            sb.AppendLine("    private static void Register(Type sourceType, Type serializedType)");
            sb.AppendLine("    {");
            sb.AppendLine("        _types.Add(new Dictionary<Type, Type> { { sourceType, serializedType } });");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private static void AppendGetSerializableTypesMethod(StringBuilder sb)
        {
            sb.AppendLine("    public static List<Dictionary<Type, Type>> GetSerializableTypes() => _types;");
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
