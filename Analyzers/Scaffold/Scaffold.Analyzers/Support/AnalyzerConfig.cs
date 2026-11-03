using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    internal static class AnalyzerConfig
    {
        /// <summary>
        /// Same diagnostic ID may map to multiple <see cref="DiagnosticDescriptor"/> instances (different title/message).
        /// The variant key disambiguates so severity overrides do not collide in the cache.
        /// </summary>
        private static readonly ConcurrentDictionary<(string Id, DiagnosticSeverity Severity, string Variant), DiagnosticDescriptor> Cache =
            new ConcurrentDictionary<(string, DiagnosticSeverity, string), DiagnosticDescriptor>();

        private static string DescriptorVariantKey(DiagnosticDescriptor d)
        {
            return string.Concat(d.Title.ToString(), "\0", d.MessageFormat.ToString());
        }

        /// <summary>
        /// When this returns <c>true</c>, <paramref name="rule"/> is the effective descriptor. When <c>false</c>, the rule is suppressed — do not use <paramref name="rule"/>.
        /// </summary>
        internal static bool TryGetEffectiveDescriptor(
            AnalyzerConfigOptions options,
            string diagnosticId,
            DiagnosticDescriptor defaultDescriptor,
            out DiagnosticDescriptor rule)
        {
            if (ShouldSuppress(options, diagnosticId))
            {
                rule = default!;
                return false;
            }

            rule = GetEffectiveDescriptor(options, diagnosticId, defaultDescriptor);
            return true;
        }

        internal static DiagnosticDescriptor GetEffectiveDescriptor(
            AnalyzerConfigOptions options,
            string diagnosticId,
            DiagnosticDescriptor defaultDescriptor)
        {
            var key = $"dotnet_diagnostic.{diagnosticId}.severity";
            if (!options.TryGetValue(key, out var raw))
                return defaultDescriptor;

            var severity = ParseSeverity(raw);
            if (severity == null || severity.Value == defaultDescriptor.DefaultSeverity)
                return defaultDescriptor;

            var variant = DescriptorVariantKey(defaultDescriptor);
            return Cache.GetOrAdd((diagnosticId, severity.Value, variant), _ =>
                new DiagnosticDescriptor(
                    defaultDescriptor.Id,
                    defaultDescriptor.Title,
                    defaultDescriptor.MessageFormat,
                    defaultDescriptor.Category,
                    severity.Value,
                    isEnabledByDefault: true,
                    description: defaultDescriptor.Description));
        }

        internal static bool ShouldSuppress(AnalyzerConfigOptions options, string diagnosticId)
        {
            var key = $"dotnet_diagnostic.{diagnosticId}.severity";
            return options.TryGetValue(key, out var raw) &&
                   raw.Trim().Equals("none", System.StringComparison.OrdinalIgnoreCase);
        }

        internal static int GetInt(AnalyzerConfigOptions options, string key, int defaultValue)
        {
            return options.TryGetValue(key, out var raw) &&
                   int.TryParse(raw.Trim(), out var v) && v > 0 ? v : defaultValue;
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="key"/> is set to a positive integer; otherwise <c>false</c> (missing, non-numeric, or non-positive).
        /// </summary>
        internal static bool TryGetPositiveInt(AnalyzerConfigOptions options, string key, out int value)
        {
            value = 0;
            if (!options.TryGetValue(key, out var raw))
            {
                return false;
            }

            if (!int.TryParse(raw.Trim(), out var v) || v <= 0)
            {
                return false;
            }

            value = v;
            return true;
        }

        /// <summary>
        /// Comma-, semicolon-, or newline-separated tokens (trimmed); empty entries skipped.
        /// Use commas in <c>.editorconfig</c> for multi-token values: Roslyn's EditorConfig parser treats
        /// unescaped <c>;</c> as the start of an end-of-line comment, so <c>key = a;b</c> is stored as <c>a</c> only.
        /// </summary>
        internal static List<string> ParseSemicolonList(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<string>();
            }

            var text = raw!;
            return text
                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        /// <summary>
        /// Parses <c>Key=Value;Key2=Value2</c> (semicolon-separated pairs). Keys and values are trimmed.
        /// </summary>
        internal static Dictionary<string, string> ParseEqualsSeparatedMap(string? raw)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return map;
            }

            foreach (var segment in raw!.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var part = segment.Trim();
                var eq = part.IndexOf('=');
                if (eq <= 0 || eq >= part.Length - 1)
                {
                    continue;
                }

                var key = part.Substring(0, eq).Trim();
                var value = part.Substring(eq + 1).Trim();
                if (key.Length == 0)
                {
                    continue;
                }

                map[key] = value;
            }

            return map;
        }

        /// <summary>
        /// Merges semicolon-list values from per-tree options (if any) then global options; order preserved, duplicates ignored (ordinal case-sensitive on token text).
        /// </summary>
        internal static List<string> MergeSemicolonOptions(
            AnalyzerConfigOptions? treeOptions,
            AnalyzerConfigOptions globalOptions,
            string key)
        {
            var result = new List<string>();
            void addFrom(AnalyzerConfigOptions? options)
            {
                if (options == null || !options.TryGetValue(key, out var raw))
                {
                    return;
                }

                foreach (var s in ParseSemicolonList(raw))
                {
                    if (result.Contains(s))
                    {
                        continue;
                    }

                    result.Add(s);
                }
            }

            addFrom(treeOptions);
            addFrom(globalOptions);
            return result;
        }

        private static DiagnosticSeverity? ParseSeverity(string raw)
        {
            switch (raw.Trim().ToLowerInvariant())
            {
                case "error": return DiagnosticSeverity.Error;
                case "warning": return DiagnosticSeverity.Warning;
                case "suggestion":
                case "info": return DiagnosticSeverity.Info;
                case "hidden":
                case "silent": return DiagnosticSeverity.Hidden;
                default: return null;
            }
        }
    }

    /// <summary>
    /// Repo-wide opt-out for Scaffold analyzers: vendor paths, optional infrastructure assemblies,
    /// configured assembly-name substrings, and configured path roots (see <c>scaffold.global.*</c> keys).
    /// Lives in this file so assemblies that link <see cref="AnalyzerConfig"/> (for example MVVM analyzers) share the same implementation.
    /// </summary>
    internal static class AnalyzerScopeGate
    {
        internal const string IgnoredAssemblyNameContainsKey = "scaffold.global.ignored_assembly_name_contains";
        internal const string IgnoredPathsKey = "scaffold.global.ignored_paths";
        internal const string IgnoreInfrastructureAssembliesKey = "scaffold.global.ignore_infrastructure_assemblies";

        /// <summary>
        /// When the compilation assembly name alone should skip all further analysis (no syntax tree yet).
        /// Uses global options only for assembly tokens; per-tree options are not available at compilation start.
        /// </summary>
        internal static bool ShouldSkipEntireCompilation(string? assemblyName, AnalyzerConfigOptions globalOptions)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                return false;
            }

            if (ShouldSkipByInfrastructureAssembly(assemblyName!, treeOptions: null, globalOptions))
            {
                return true;
            }

            return MatchesAnyAssemblyToken(
                assemblyName!,
                AnalyzerConfig.MergeSemicolonOptions(null, globalOptions, IgnoredAssemblyNameContainsKey));
        }

        /// <summary>
        /// Full gate for a source file: vendor, infrastructure (optional), configured paths, configured assembly tokens.
        /// </summary>
        internal static bool ShouldSkipAllScaffoldRules(
            string? assemblyName,
            string? filePath,
            AnalyzerConfigOptions? treeOptions,
            AnalyzerConfigOptions globalOptions)
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                var path = filePath!;
                if (ModuleConventions.IsExcludedThirdPartyVendorPath(path))
                {
                    return true;
                }

                var normalized = ScriptPathFilters.Normalize(path);
                if (ScriptPathFilters.IsPathUnderAnyConfiguredIgnoredRoot(
                        normalized,
                        AnalyzerConfig.MergeSemicolonOptions(treeOptions, globalOptions, IgnoredPathsKey)))
                {
                    return true;
                }
            }

            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                return false;
            }

            if (ShouldSkipByInfrastructureAssembly(assemblyName!, treeOptions, globalOptions))
            {
                return true;
            }

            return MatchesAnyAssemblyToken(
                assemblyName!,
                AnalyzerConfig.MergeSemicolonOptions(treeOptions, globalOptions, IgnoredAssemblyNameContainsKey));
        }

        private static bool ShouldSkipByInfrastructureAssembly(
            string assemblyName,
            AnalyzerConfigOptions? treeOptions,
            AnalyzerConfigOptions globalOptions)
        {
            string? raw = null;
            if (treeOptions != null &&
                treeOptions.TryGetValue(IgnoreInfrastructureAssembliesKey, out var treeRaw) &&
                !string.IsNullOrWhiteSpace(treeRaw))
            {
                raw = treeRaw;
            }
            else if (globalOptions.TryGetValue(IgnoreInfrastructureAssembliesKey, out var globalRaw) &&
                     !string.IsNullOrWhiteSpace(globalRaw))
            {
                raw = globalRaw;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var v = raw!.Trim();
            if (v.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                v.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                v.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                return ModuleConventions.IsInfrastructureAssembly(assemblyName);
            }

            return false;
        }

        private static bool MatchesAnyAssemblyToken(string assemblyName, IReadOnlyList<string> tokens)
        {
            foreach (var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (assemblyName.IndexOf(token.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool ShouldSkipSyntaxNodeAnalysis(SyntaxNodeAnalysisContext context)
        {
            var provider = context.Options.AnalyzerConfigOptionsProvider;
            var tree = context.Node.SyntaxTree;
            return ShouldSkipAllScaffoldRules(
                context.SemanticModel.Compilation.AssemblyName,
                tree.FilePath,
                provider.GetOptions(tree),
                provider.GlobalOptions);
        }

        internal static bool ShouldSkipSymbolAnalysis(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol)
        {
            var provider = context.Options.AnalyzerConfigOptionsProvider;
            var assemblyName = typeSymbol.ContainingAssembly?.Name;
            var sourceLoc = typeSymbol.Locations.FirstOrDefault(l => l.SourceTree != null);
            if (sourceLoc?.SourceTree == null)
            {
                return false;
            }

            var tree = sourceLoc.SourceTree;
            return ShouldSkipAllScaffoldRules(
                assemblyName,
                tree.FilePath,
                provider.GetOptions(tree),
                provider.GlobalOptions);
        }
    }
}
