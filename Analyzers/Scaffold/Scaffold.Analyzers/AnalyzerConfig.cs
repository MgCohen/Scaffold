using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    internal static class AnalyzerConfig
    {
        private static readonly ConcurrentDictionary<(string, DiagnosticSeverity), DiagnosticDescriptor> Cache = new ConcurrentDictionary<(string, DiagnosticSeverity), DiagnosticDescriptor>();

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

            return Cache.GetOrAdd((diagnosticId, severity.Value), _ =>
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

        private static DiagnosticSeverity? ParseSeverity(string raw)
        {
            switch (raw.Trim().ToLowerInvariant())
            {
                case "error":      return DiagnosticSeverity.Error;
                case "warning":    return DiagnosticSeverity.Warning;
                case "suggestion":
                case "info":       return DiagnosticSeverity.Info;
                case "hidden":
                case "silent":     return DiagnosticSeverity.Hidden;
                default:           return null;
            }
        }
    }
}
