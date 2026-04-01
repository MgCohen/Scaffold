using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace Scaffold.Analyzers
{
    internal static class InvariantUsageScope
    {
        private static readonly ConcurrentDictionary<string, UsageIndex> Cache = new ConcurrentDictionary<string, UsageIndex>(StringComparer.OrdinalIgnoreCase);
        private static readonly Regex NameRegex = new Regex("\"name\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex ReferencesRegex = new Regex("\"references\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex QuotedValueRegex = new Regex("\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex IdentifierRegex = new Regex("\\b[A-Za-z_][A-Za-z0-9_]*\\b", RegexOptions.Compiled);

        internal static bool ShouldValidateForType(Compilation compilation, INamedTypeSymbol containingType)
        {
            if (compilation == null || containingType == null) return false;

            UsageIndex index = GetOrCreateUsageIndex(compilation);
            if (!index.IsAvailable) return false;
            if (index.ExternallyMentionedTypeNames.Contains(containingType.Name)) return true;

            if (containingType.TypeKind != TypeKind.Class) return false;

            foreach (INamedTypeSymbol implementedInterface in containingType.AllInterfaces)
            {
                if (index.ExternallyMentionedTypeNames.Contains(implementedInterface.Name))
                {
                    return true;
                }
            }

            return false;
        }

        private static UsageIndex GetOrCreateUsageIndex(Compilation compilation)
        {
            string cacheKey = BuildCacheKey(compilation);
            return Cache.GetOrAdd(cacheKey, _ => BuildUsageIndex(compilation));
        }

        private static string BuildCacheKey(Compilation compilation)
        {
            string assemblyName = compilation.AssemblyName ?? string.Empty;
            string root = TryGetRepositoryRoot(compilation) ?? string.Empty;
            return root + "|" + assemblyName;
        }

        private static UsageIndex BuildUsageIndex(Compilation compilation)
        {
            string assemblyName = compilation.AssemblyName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(assemblyName)) return UsageIndex.Unavailable;

            string repositoryRoot = TryGetRepositoryRoot(compilation);
            if (string.IsNullOrWhiteSpace(repositoryRoot)) return UsageIndex.Unavailable;

            string assetsScriptsRoot = Path.Combine(repositoryRoot, "Assets", "Scripts");
            if (!Directory.Exists(assetsScriptsRoot)) return UsageIndex.Unavailable;

            IReadOnlyList<AsmdefInfo> asmdefs = LoadAsmdefs(assetsScriptsRoot);
            if (asmdefs.Count == 0) return UsageIndex.Unavailable;

            string currentModuleRoot = ModuleConventions.GetModuleRootName(assemblyName);
            var externalConsumerDirectories = asmdefs
                .Where(asmdef => asmdef.References.Contains(assemblyName))
                .Where(asmdef => IsExternalConsumerAssembly(asmdef.Name, currentModuleRoot))
                .Select(asmdef => asmdef.DirectoryPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (externalConsumerDirectories.Length == 0)
            {
                return UsageIndex.Empty;
            }

            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            foreach (string directory in externalConsumerDirectories)
            {
                AddIdentifiersFromDirectory(directory, identifiers);
            }

            return new UsageIndex(identifiers);
        }

        private static bool IsExternalConsumerAssembly(string consumerAssemblyName, string currentModuleRoot)
        {
            if (string.IsNullOrWhiteSpace(consumerAssemblyName)) return false;
            if (consumerAssemblyName.EndsWith(".Container", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (ModuleConventions.IsInfrastructureAssembly(consumerAssemblyName))
            {
                return false;
            }

            string consumerModuleRoot = ModuleConventions.GetModuleRootName(consumerAssemblyName);
            return !string.Equals(consumerModuleRoot, currentModuleRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<AsmdefInfo> LoadAsmdefs(string assetsScriptsRoot)
        {
            var result = new List<AsmdefInfo>();
            foreach (string asmdefPath in Directory.EnumerateFiles(assetsScriptsRoot, "*.asmdef", SearchOption.AllDirectories))
            {
                string content;
                try
                {
                    content = File.ReadAllText(asmdefPath);
                }
                catch
                {
                    continue;
                }

                if (!TryParseAsmdefName(content, out string name)) continue;
                HashSet<string> references = ParseAsmdefReferences(content);
                string directoryPath = Path.GetDirectoryName(asmdefPath) ?? string.Empty;
                result.Add(new AsmdefInfo(name, references, directoryPath));
            }

            return result;
        }

        private static bool TryParseAsmdefName(string content, out string name)
        {
            Match match = NameRegex.Match(content);
            if (!match.Success)
            {
                name = string.Empty;
                return false;
            }

            name = match.Groups[1].Value.Trim();
            return !string.IsNullOrWhiteSpace(name);
        }

        private static HashSet<string> ParseAsmdefReferences(string content)
        {
            var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Match blockMatch = ReferencesRegex.Match(content);
            if (!blockMatch.Success) return references;

            MatchCollection referenceMatches = QuotedValueRegex.Matches(blockMatch.Groups[1].Value);
            foreach (Match referenceMatch in referenceMatches)
            {
                string value = referenceMatch.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(value)) continue;
                references.Add(value);
            }

            return references;
        }

        private static void AddIdentifiersFromDirectory(string directoryPath, ISet<string> identifiers)
        {
            if (!Directory.Exists(directoryPath)) return;

            IEnumerable<string> sourceFiles = Directory.EnumerateFiles(directoryPath, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsGeneratedPath(path));
            foreach (string sourceFile in sourceFiles)
            {
                string text;
                try
                {
                    text = File.ReadAllText(sourceFile);
                }
                catch
                {
                    continue;
                }

                MatchCollection matches = IdentifierRegex.Matches(text);
                foreach (Match match in matches)
                {
                    identifiers.Add(match.Value);
                }
            }
        }

        private static bool IsGeneratedPath(string filePath)
        {
            string normalized = filePath.Replace('\\', '/');
            if (normalized.IndexOf("/obj/", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (normalized.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (normalized.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string TryGetRepositoryRoot(Compilation compilation)
        {
            foreach (SyntaxTree syntaxTree in compilation.SyntaxTrees)
            {
                string filePath = syntaxTree.FilePath;
                if (string.IsNullOrWhiteSpace(filePath)) continue;

                string normalized = filePath.Replace('\\', '/');
                int tokenIndex = normalized.IndexOf("/Assets/Scripts/", StringComparison.OrdinalIgnoreCase);
                if (tokenIndex < 0) continue;

                string root = normalized.Substring(0, tokenIndex);
                if (string.IsNullOrWhiteSpace(root)) continue;
                return Path.GetFullPath(root);
            }

            return string.Empty;
        }

        private sealed class UsageIndex
        {
            internal static UsageIndex Unavailable { get; } = new UsageIndex(false, new HashSet<string>(StringComparer.Ordinal));
            internal static UsageIndex Empty { get; } = new UsageIndex(true, new HashSet<string>(StringComparer.Ordinal));

            internal UsageIndex(ISet<string> externallyMentionedTypeNames)
                : this(true, new HashSet<string>(externallyMentionedTypeNames, StringComparer.Ordinal))
            {
            }

            private UsageIndex(bool isAvailable, HashSet<string> externallyMentionedTypeNames)
            {
                IsAvailable = isAvailable;
                ExternallyMentionedTypeNames = externallyMentionedTypeNames;
            }

            internal bool IsAvailable { get; }
            internal HashSet<string> ExternallyMentionedTypeNames { get; }
        }

        private sealed class AsmdefInfo
        {
            internal AsmdefInfo(string name, HashSet<string> references, string directoryPath)
            {
                Name = name;
                References = references;
                DirectoryPath = directoryPath;
            }

            internal string Name { get; }
            internal HashSet<string> References { get; }
            internal string DirectoryPath { get; }
        }
    }
}
