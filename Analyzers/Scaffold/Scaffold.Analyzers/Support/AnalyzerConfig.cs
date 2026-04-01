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
        /// Semicolon- or newline-separated tokens (trimmed); empty entries skipped.
        /// </summary>
        internal static List<string> ParseSemicolonList(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<string>();
            }

            var text = raw!;
            return text
                .Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
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
