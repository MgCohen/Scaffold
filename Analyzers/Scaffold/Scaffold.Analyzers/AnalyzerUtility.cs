using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    public static class AnalyzerUtility
    {
        public static bool IsInAssetsFolder(SyntaxNode node)
        {
            if (node?.SyntaxTree?.FilePath == null)
                return false;

            string path = node.SyntaxTree.FilePath.Replace('\\', '/');
            return path.Contains("/Assets/") || path.StartsWith("Assets/");
        }

        public static bool IsInAssetsFolder(SyntaxTree tree)
        {
            if (tree?.FilePath == null)
                return false;

            string path = tree.FilePath.Replace('\\', '/');
            return path.Contains("/Assets/") || path.StartsWith("Assets/");
        }

        public static int GetIntOption(AnalyzerConfigOptions options, string key, int defaultValue)
        {
            if (options.TryGetValue(key, out var value) && int.TryParse(value, out var result))
            {
                return result;
            }
            return defaultValue;
        }

        public static string GetStringOption(AnalyzerConfigOptions options, string key, string defaultValue)
        {
            if (options.TryGetValue(key, out var value))
            {
                return value;
            }
            return defaultValue;
        }
    }
}
