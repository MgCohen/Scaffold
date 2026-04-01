using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Scaffold.Analyzers
{
    internal static class ModuleConventions
    {
        internal sealed class ModuleContext
        {
            internal string AssemblyName { get; set; } = string.Empty;
            internal string ModuleRootName { get; set; } = string.Empty;
            internal string ModuleDirectoryPath { get; set; } = string.Empty;
            internal Location DiagnosticLocation { get; set; } = Location.None;
            internal SyntaxTree? SyntaxTree { get; set; }
        }

        internal static bool TryGetModuleContext(Compilation compilation, out ModuleContext context)
        {
            context = new ModuleContext();

            var assemblyName = compilation.AssemblyName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(assemblyName)) return false;

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var filePath = syntaxTree.FilePath;
                if (string.IsNullOrWhiteSpace(filePath)) continue;

                if (!TryGetModuleDirectoryPath(filePath, out var moduleDirectoryPath))
                {
                    continue;
                }

                context = new ModuleContext
                {
                    AssemblyName = assemblyName,
                    ModuleRootName = GetModuleRootName(assemblyName),
                    ModuleDirectoryPath = moduleDirectoryPath,
                    DiagnosticLocation = syntaxTree.GetRoot().GetLocation(),
                    SyntaxTree = syntaxTree
                };

                return true;
            }

            return false;
        }

        internal static bool IsInfrastructureAssembly(string assemblyName)
        {
            return assemblyName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.EndsWith(".PlayModeTests", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.EndsWith(".Samples", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.EndsWith(".Container", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.EndsWith(".Editor", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Third-party sources shipped under Assets/Plugins (for example DOTween module scripts) are not project-owned code and must not be analyzed by Scaffold rules.
        /// </summary>
        internal static bool IsExcludedThirdPartyVendorPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            var normalized = ScriptPathFilters.Normalize(filePath);
            return normalized.IndexOf("/Assets/Plugins/Demigiant/DOTween/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static string GetModuleRootName(string assemblyName)
        {
            var suffixes = new[]
            {
                ".Runtime",
                ".Contracts",
                ".Container",
                ".Editor",
                ".Samples",
                ".Tests",
                ".PlayModeTests"
            };

            var match = suffixes.FirstOrDefault(suffix => assemblyName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            if (match == null) return assemblyName;
            return assemblyName.Substring(0, assemblyName.Length - match.Length);
        }

        private static bool TryGetModuleDirectoryPath(string filePath, out string moduleDirectoryPath)
        {
            var normalized = ScriptPathFilters.Normalize(filePath);
            if (!ScriptPathFilters.TryGetPathAfterAssetsScripts(normalized, out var afterToken))
            {
                moduleDirectoryPath = string.Empty;
                return false;
            }

            var assetsScriptsAbsolutePath = normalized.Substring(0, normalized.Length - afterToken.Length).TrimEnd('/');
            var segments = afterToken.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
            {
                moduleDirectoryPath = string.Empty;
                return false;
            }

            var moduleRelativePath = $"{segments[0]}/{segments[1]}";
            moduleDirectoryPath = Path.GetFullPath(Path.Combine(assetsScriptsAbsolutePath, moduleRelativePath));
            return true;
        }
    }
}
