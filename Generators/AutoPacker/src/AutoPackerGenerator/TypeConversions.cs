using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace AutoPackerGenerator
{
    internal static class TypeConversions
    {
        // Maps field type display string → fully qualified target type for the Packed struct.
        // Add entries here whenever a new non-blittable type needs a packable equivalent.
        private static readonly Dictionary<string, string> Map = new Dictionary<string, string>
        {
            { "string", "Unity.Collections.FixedString128Bytes" },
        };

        public static bool TryGetConversion(ITypeSymbol fieldType, out string targetTypeFull)
        {
            return Map.TryGetValue(fieldType.ToDisplayString(), out targetTypeFull);
        }

        public static string GetShortName(string fullyQualifiedTypeName)
        {
            var dot = fullyQualifiedTypeName.LastIndexOf('.');
            return dot >= 0 ? fullyQualifiedTypeName.Substring(dot + 1) : fullyQualifiedTypeName;
        }

        public static string GetNamespace(string fullyQualifiedTypeName)
        {
            var dot = fullyQualifiedTypeName.LastIndexOf('.');
            return dot >= 0 ? fullyQualifiedTypeName.Substring(0, dot) : null;
        }
    }
}
