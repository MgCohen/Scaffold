using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ModuleAsmdefConventionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA4003";
        private const string Category = "Architecture";
        private const string ExemptAssembliesKey = "scaffold.SCA4003.exempt_assemblies";
        private const string SuffixFolderMapKey = "scaffold.SCA4003.suffix_folder_map";
        private const string DisallowModuleRootAsmdefKey = "scaffold.SCA4003.disallow_module_root_asmdef";
        private const string AllowUnknownSuffixInAnySubfolderKey = "scaffold.SCA4003.allow_unknown_suffix_in_any_subfolder";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Module asmdef must match placement and name convention",
            "Error SCA4003: Assembly '{0}' must declare asmdef at '{1}' with name '{0}'",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Asmdef placement/name should match module root convention to keep module ownership and generated projects stable.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationAction(AnalyzeCompilation);
        }

        private static void AnalyzeCompilation(CompilationAnalysisContext context)
        {
            if (!ModuleConventions.TryGetModuleContext(context.Compilation, out var moduleContext))
            {
                return;
            }

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(moduleContext.SyntaxTree);
            if (AnalyzerConfig.ShouldSuppress(options, DiagnosticId)) return;
            var rule = AnalyzerConfig.GetEffectiveDescriptor(options, DiagnosticId, Rule);

            var exemptAssemblies = ParseSet(options, ExemptAssembliesKey);
            if (exemptAssemblies.Contains(moduleContext.AssemblyName)) return;

            var fileNames = GetCandidateFileNames(moduleContext.AssemblyName, moduleContext.ModuleRootName);
            var disallowModuleRootAsmdef = ParseBool(options, DisallowModuleRootAsmdefKey, true);
            if (disallowModuleRootAsmdef && HasRootAsmdef(moduleContext.ModuleDirectoryPath, fileNames))
            {
                var expectedPath = ResolveExpectedPath(moduleContext, options, fileNames);
                context.ReportDiagnostic(Diagnostic.Create(rule, moduleContext.DiagnosticLocation, moduleContext.AssemblyName, expectedPath));
                return;
            }

            var candidatePaths = GetCandidateAsmdefPaths(moduleContext, options, fileNames);
            var asmdefPath = candidatePaths.FirstOrDefault(FileExists);
            if (string.IsNullOrWhiteSpace(asmdefPath))
            {
                var expectedPath = candidatePaths.First();
                context.ReportDiagnostic(Diagnostic.Create(rule, moduleContext.DiagnosticLocation, moduleContext.AssemblyName, expectedPath));
                return;
            }

            var asmdefName = TryReadAsmdefName(asmdefPath);
            if (!string.Equals(asmdefName, moduleContext.AssemblyName, StringComparison.Ordinal) &&
                !string.Equals(asmdefName, moduleContext.ModuleRootName, StringComparison.Ordinal))
            {
                context.ReportDiagnostic(Diagnostic.Create(rule, moduleContext.DiagnosticLocation, moduleContext.AssemblyName, asmdefPath));
            }
        }

        private static IReadOnlyList<string> GetCandidateAsmdefPaths(ModuleConventions.ModuleContext moduleContext, AnalyzerConfigOptions options, IReadOnlyList<string> fileNames)
        {
            var suffixMap = GetSuffixFolderMap(options);
            if (TryResolveFolders(moduleContext.AssemblyName, suffixMap, out var folders))
            {
                return ComposeCandidatePaths(moduleContext.ModuleDirectoryPath, folders, fileNames);
            }

            var allowUnknownInAnySubfolder = ParseBool(options, AllowUnknownSuffixInAnySubfolderKey, false);
            if (allowUnknownInAnySubfolder)
            {
                return EnumerateUnknownSuffixCandidates(moduleContext.ModuleDirectoryPath, fileNames);
            }

            var defaultFolders = new[] { "Runtime" };
            return ComposeCandidatePaths(moduleContext.ModuleDirectoryPath, defaultFolders, fileNames);
        }

        private static string ResolveExpectedPath(ModuleConventions.ModuleContext moduleContext, AnalyzerConfigOptions options, IReadOnlyList<string> fileNames)
        {
            var candidates = GetCandidateAsmdefPaths(moduleContext, options, fileNames);
            return candidates.First();
        }

        private static IReadOnlyList<string> GetCandidateFileNames(string assemblyName, string moduleRootName)
        {
            return new[] { assemblyName + ".asmdef", moduleRootName + ".asmdef" }
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool HasRootAsmdef(string moduleDirectoryPath, IReadOnlyList<string> fileNames)
        {
            for (var i = 0; i < fileNames.Count; i++)
            {
                var path = Path.Combine(moduleDirectoryPath, fileNames[i]);
                if (FileExists(path))
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<string> ComposeCandidatePaths(string moduleDirectoryPath, IReadOnlyList<string> folders, IReadOnlyList<string> fileNames)
        {
            return folders
                .SelectMany(folder =>
                    fileNames.Select(fileName =>
                        string.IsNullOrWhiteSpace(folder)
                            ? Path.Combine(moduleDirectoryPath, fileName)
                            : Path.Combine(moduleDirectoryPath, folder, fileName)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static IReadOnlyList<string> EnumerateUnknownSuffixCandidates(string moduleDirectoryPath, IReadOnlyList<string> fileNames)
        {
            var results = new List<string>();
#pragma warning disable RS1035
            var directories = Directory.EnumerateDirectories(moduleDirectoryPath, "*", SearchOption.AllDirectories);
#pragma warning restore RS1035
            foreach (var directory in directories)
            {
                if (string.Equals(directory, moduleDirectoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                for (var i = 0; i < fileNames.Count; i++)
                {
                    results.Add(Path.Combine(directory, fileNames[i]));
                }
            }

            if (results.Count > 0)
            {
                return results.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }

            return new[] { Path.Combine(moduleDirectoryPath, "<subfolder>", fileNames[0]) };
        }

        private static bool TryResolveFolders(string assemblyName, IReadOnlyDictionary<string, string[]> suffixMap, out string[] folders)
        {
            var matchedSuffix = suffixMap.Keys
                .Where(suffix => assemblyName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(suffix => suffix.Length)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(matchedSuffix))
            {
                folders = suffixMap[matchedSuffix];
                return true;
            }

            folders = Array.Empty<string>();
            return false;
        }

        private static IReadOnlyDictionary<string, string[]> GetSuffixFolderMap(AnalyzerConfigOptions options)
        {
            var defaults = CreateDefaultSuffixFolderMap();
            if (!options.TryGetValue(SuffixFolderMapKey, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return defaults;
            }

            var configured = ParseSuffixFolderMap(raw);
            if (configured.Count == 0)
            {
                return defaults;
            }

            foreach (var pair in defaults)
            {
                if (!configured.ContainsKey(pair.Key))
                {
                    configured[pair.Key] = pair.Value;
                }
            }

            return configured;
        }

        private static Dictionary<string, string[]> CreateDefaultSuffixFolderMap()
        {
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [".Runtime"] = new[] { "Runtime" },
                [".Tests"] = new[] { "Tests" },
                [".PlayModeTests"] = new[] { Path.Combine("Tests", "PlayMode") },
                [".Samples"] = new[] { "Samples" },
                [".Container"] = new[] { "Container" },
                [".Editor"] = new[] { "Editor" }
            };
        }

        private static Dictionary<string, string[]> ParseSuffixFolderMap(string raw)
        {
            var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            var pairs = raw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var kv = pair.Split(new[] { '=' }, 2);
                if (kv.Length != 2)
                {
                    continue;
                }

                var suffix = kv[0].Trim();
                var folderRaw = kv[1].Trim();
                if (string.IsNullOrWhiteSpace(suffix) || string.IsNullOrWhiteSpace(folderRaw))
                {
                    continue;
                }

                if (!suffix.StartsWith(".", StringComparison.Ordinal))
                {
                    suffix = "." + suffix;
                }

                var folders = folderRaw
                    .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(folder => folder.Trim())
                    .Where(folder => !string.IsNullOrWhiteSpace(folder))
                    .ToArray();
                if (folders.Length == 0)
                {
                    continue;
                }

                result[suffix] = folders;
            }

            return result;
        }

        private static bool ParseBool(AnalyzerConfigOptions options, string key, bool defaultValue)
        {
            if (!options.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            if (bool.TryParse(raw, out var parsed))
            {
                return parsed;
            }

            switch (raw.Trim().ToLowerInvariant())
            {
                case "1":
                case "yes":
                case "y":
                    return true;
                case "0":
                case "no":
                case "n":
                    return false;
                default:
                    return defaultValue;
            }
        }

        private static bool FileExists(string path)
        {
#pragma warning disable RS1035
            return File.Exists(path);
#pragma warning restore RS1035
        }

        private static string TryReadAsmdefName(string asmdefPath)
        {
            try
            {
#pragma warning disable RS1035
                var content = File.ReadAllText(asmdefPath);
#pragma warning restore RS1035
                var match = Regex.Match(content, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                return match.Success ? match.Groups[1].Value : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static HashSet<string> ParseSet(AnalyzerConfigOptions options, string key)
        {
            if (!options.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>(
                raw.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => item.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item)),
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
