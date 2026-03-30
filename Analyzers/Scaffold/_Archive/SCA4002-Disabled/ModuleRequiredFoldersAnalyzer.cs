using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ModuleRequiredFoldersAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA4002";
        private const string Category = "Architecture";
        private const string RequiredFoldersKey = "scaffold.SCA4002.required_folders";
        private const string ExemptModulesKey = "scaffold.SCA4002.exempt_module_roots";
        private const string ExemptRequirementsKey = "scaffold.SCA4002.exempt_requirements";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Required module folders must exist",
            "Error SCA4002: Module '{0}' is missing required folder '{1}' (expected at '{2}')",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Modules must contain required top-level folders (default: Runtime, Tests). Use analyzer config exceptions for intentional deviations.");

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

            var exemptModuleRoots = ParseSet(options, ExemptModulesKey);
            if (exemptModuleRoots.Contains(moduleContext.ModuleRootName))
            {
                return;
            }

            var requiredFolders = ParseRequiredFolders(options);
            var exemptRequirements = ParseModuleFolderExemptions(options);
            exemptRequirements.TryGetValue(moduleContext.ModuleRootName, out var moduleSpecificExemptions);
            moduleSpecificExemptions = moduleSpecificExemptions ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var requiredFolder in requiredFolders)
            {
                if (moduleSpecificExemptions.Contains(requiredFolder)) continue;

                var expectedFolderPath = Path.Combine(moduleContext.ModuleDirectoryPath, requiredFolder);
#pragma warning disable RS1035
                if (Directory.Exists(expectedFolderPath)) continue;
#pragma warning restore RS1035

                var diagnostic = Diagnostic.Create(
                    rule,
                    moduleContext.DiagnosticLocation,
                    moduleContext.ModuleRootName,
                    requiredFolder,
                    expectedFolderPath);

                context.ReportDiagnostic(diagnostic);
            }
        }

        private static IReadOnlyList<string> ParseRequiredFolders(AnalyzerConfigOptions options)
        {
            if (!options.TryGetValue(RequiredFoldersKey, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return new[] { "Runtime", "Tests" };
            }

            var parsed = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return parsed.Length == 0 ? new[] { "Runtime", "Tests" } : parsed;
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

        private static Dictionary<string, HashSet<string>> ParseModuleFolderExemptions(AnalyzerConfigOptions options)
        {
            var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            if (!options.TryGetValue(ExemptRequirementsKey, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            var entries = raw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var parts = entry.Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2) continue;

                var moduleRoot = parts[0].Trim();
                if (string.IsNullOrWhiteSpace(moduleRoot)) continue;

                var folders = parts[1]
                    .Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(folder => folder.Trim())
                    .Where(folder => !string.IsNullOrWhiteSpace(folder));

                result[moduleRoot] = new HashSet<string>(folders, StringComparer.OrdinalIgnoreCase);
            }

            return result;
        }
    }
}
