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
    public sealed class RuntimeAssemblyBoundaryAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA4001";
        private const string Category = "Architecture";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Restrict cross-module runtime assembly references",
            "Error SCA4001: Assembly '{0}' references runtime assembly '{1}'. Non-bootstrap modules must avoid cross-module '*.Runtime' references.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Only composition root assemblies should reference foreign runtime assemblies. Unified module assemblies are allowed when no separate runtime assembly exists.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationAction(AnalyzeCompilation);
        }

        private void AnalyzeCompilation(CompilationAnalysisContext context)
        {
            var options = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
            if (AnalyzerConfig.ShouldSuppress(options, DiagnosticId)) return;
            var rule = AnalyzerConfig.GetEffectiveDescriptor(options, DiagnosticId, Rule);

            var assemblyName = context.Compilation.AssemblyName;
            if (string.IsNullOrWhiteSpace(assemblyName)) return;
            if (IsBootstrapAssembly(assemblyName)) return;
            if (ModuleConventions.IsInfrastructureAssembly(assemblyName)) return;

            var currentModuleRoot = ModuleConventions.GetModuleRootName(assemblyName);
            var moduleRootsWithoutContracts = ParseModuleRootsWithoutContracts(options);
            string assetsScriptsRoot = TryGetAssetsScriptsRoot(context.Compilation);
            foreach (var reference in context.Compilation.ReferencedAssemblyNames)
            {
                var referenceName = reference?.Name;
                if (!IsRuntimeAssemblyName(referenceName)) continue;
                if (IsSameModule(currentModuleRoot, referenceName)) continue;
                if (ShouldSkipForNoContractsException(referenceName, assetsScriptsRoot, moduleRootsWithoutContracts)) continue;

                var location = GetDiagnosticLocation(context.Compilation);
                var diagnostic = Diagnostic.Create(rule, location, assemblyName, referenceName);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool IsBootstrapAssembly(string assemblyName)
        {
            return assemblyName.IndexOf(".Bootstrap", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   assemblyName.EndsWith("Bootstrap", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRuntimeAssemblyName(string assemblyName)
        {
            return IsModuleRuntimeAssembly(assemblyName) &&
                   assemblyName.EndsWith(".Runtime", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsModuleRuntimeAssembly(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName)) return false;
            return assemblyName.StartsWith("Scaffold.", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.StartsWith("Scaffold.", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameModule(string currentModuleRoot, string referencedAssemblyName)
        {
            var referencedRoot = ModuleConventions.GetModuleRootName(referencedAssemblyName);
            return string.Equals(currentModuleRoot, referencedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSkipForNoContractsException(string referencedAssemblyName, string assetsScriptsRoot, ISet<string> configuredModulesWithoutContracts)
        {
            var referencedModuleRoot = ModuleConventions.GetModuleRootName(referencedAssemblyName);
            if (configuredModulesWithoutContracts.Contains(referencedModuleRoot))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(assetsScriptsRoot))
            {
                return false;
            }

            if (!TryGetReferencedModuleDirectory(assetsScriptsRoot, referencedAssemblyName, out var referencedModuleDirectory))
            {
                return false;
            }

            return !HasContractsSurface(referencedModuleDirectory, referencedModuleRoot);
        }

        private static string TryGetAssetsScriptsRoot(Compilation compilation)
        {
            if (!ModuleConventions.TryGetModuleContext(compilation, out var moduleContext))
            {
                return string.Empty;
            }

            var moduleDirectory = moduleContext.ModuleDirectoryPath;
            if (string.IsNullOrWhiteSpace(moduleDirectory))
            {
                return string.Empty;
            }

            var moduleInfo = new DirectoryInfo(moduleDirectory);
            var assetsScripts = moduleInfo.Parent?.Parent;
            if (assetsScripts == null)
            {
                return string.Empty;
            }

            return assetsScripts.FullName;
        }

        private static bool TryGetReferencedModuleDirectory(string assetsScriptsRoot, string referencedAssemblyName, out string moduleDirectoryPath)
        {
            moduleDirectoryPath = string.Empty;
            if (!Directory.Exists(assetsScriptsRoot))
            {
                return false;
            }

            var runtimeAsmdefName = referencedAssemblyName + ".asmdef";
            var runtimeAsmdefPath = Directory
                .EnumerateFiles(assetsScriptsRoot, runtimeAsmdefName, SearchOption.AllDirectories)
                .FirstOrDefault(path => path.Replace('\\', '/').IndexOf("/Runtime/", StringComparison.OrdinalIgnoreCase) >= 0);

            if (string.IsNullOrWhiteSpace(runtimeAsmdefPath))
            {
                return false;
            }

            var runtimeDirectory = Path.GetDirectoryName(runtimeAsmdefPath);
            if (string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                return false;
            }

            var moduleDirectory = Directory.GetParent(runtimeDirectory);
            moduleDirectoryPath = moduleDirectory?.FullName ?? runtimeDirectory;
            return !string.IsNullOrWhiteSpace(moduleDirectoryPath);
        }

        private static bool HasContractsSurface(string moduleDirectoryPath, string moduleRootName)
        {
            var contractsFolderPath = Path.Combine(moduleDirectoryPath, "Contracts");
            if (!Directory.Exists(contractsFolderPath))
            {
                return false;
            }

            var contractsAsmdefPath = Path.Combine(contractsFolderPath, moduleRootName + ".Contracts.asmdef");
            if (File.Exists(contractsAsmdefPath))
            {
                return true;
            }

            return Directory.EnumerateFiles(contractsFolderPath, "*.asmdef", SearchOption.TopDirectoryOnly).Any();
        }

        private static ISet<string> ParseModuleRootsWithoutContracts(AnalyzerConfigOptions options)
        {
            const string key = "scaffold.SCA4001.no_contract_modules";
            if (!options.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var entries = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            return new HashSet<string>(entries.Select(entry => entry.Trim()).Where(entry => !string.IsNullOrWhiteSpace(entry)), StringComparer.OrdinalIgnoreCase);
        }

        private static Location GetDiagnosticLocation(Compilation compilation)
        {
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var root = syntaxTree.GetRoot();
                return root.GetLocation();
            }

            return Location.None;
        }
    }
}
