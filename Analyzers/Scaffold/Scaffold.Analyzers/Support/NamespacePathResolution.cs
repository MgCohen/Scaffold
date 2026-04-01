using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    /// <summary>
    /// Shared path and namespace layout logic for SCA3005 / SCA3006.
    /// </summary>
    internal static class NamespacePathResolution
    {
        public const string RootKey = "scaffold.SCA3005.root";
        public const string AllowedRootsKey = "scaffold.SCA3005.allowed_roots";
        public const string ContentRootsKey = "scaffold.SCA3006.content_roots";
        public const string FirstSegmentIgnoreKey = "scaffold.SCA3006.first_segment_ignore";
        public const string SuffixIgnoreGlobsKey = "scaffold.SCA3006.suffix_ignore_globs";

        public static HashSet<string> GetEffectiveAllowedRoots(
            AnalyzerConfigOptions treeOptions,
            AnalyzerConfigOptions globalOptions)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (TryGetNonEmptyFromTreeOrGlobal(treeOptions, globalOptions, RootKey, out var root))
            {
                set.Add(root);
            }

            var allowedRaw = TryGetAllowedRootsRawFromTreeOrGlobal(treeOptions, globalOptions);
            foreach (var token in AnalyzerConfig.ParseSemicolonList(allowedRaw))
            {
                var t = token.Trim();
                if (t.Length > 0)
                {
                    set.Add(t);
                }
            }

            return set;
        }

        private static string? TryGetAllowedRootsRawFromTreeOrGlobal(
            AnalyzerConfigOptions treeOptions,
            AnalyzerConfigOptions globalOptions)
        {
            if (treeOptions.TryGetValue(AllowedRootsKey, out var raw) && !string.IsNullOrWhiteSpace(raw))
            {
                return raw;
            }

            return globalOptions.TryGetValue(AllowedRootsKey, out raw) && !string.IsNullOrWhiteSpace(raw) ? raw : null;
        }

        public static bool TryGetNonEmptyFromTreeOrGlobal(
            AnalyzerConfigOptions treeOptions,
            AnalyzerConfigOptions globalOptions,
            string key,
            out string value)
        {
            if (TryGetNonEmpty(treeOptions, key, out value))
            {
                return true;
            }

            return TryGetNonEmpty(globalOptions, key, out value);
        }

        private static bool TryGetNonEmpty(AnalyzerConfigOptions options, string key, out string value)
        {
            if (options.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw))
            {
                value = raw.Trim();
                return true;
            }

            value = string.Empty;
            return false;
        }

        public static IReadOnlyList<string> GetContentRoots(AnalyzerConfigOptions treeOptions, AnalyzerConfigOptions globalOptions)
        {
            if (TryGetNonEmptyFromTreeOrGlobal(treeOptions, globalOptions, ContentRootsKey, out var raw) &&
                !string.IsNullOrWhiteSpace(raw))
            {
                var list = AnalyzerConfig.ParseSemicolonList(raw);
                return list.Count > 0 ? list : new[] { "Assets/Scripts" };
            }

            return new[] { "Assets/Scripts" };
        }

        public static bool TryGetFolderSegmentsAfterContentRoot(
            string filePath,
            AnalyzerConfigOptions treeOptions,
            AnalyzerConfigOptions globalOptions,
            out IReadOnlyList<string> folderSegments)
        {
            folderSegments = Array.Empty<string>();
            var normalized = ScriptPathFilters.Normalize(filePath);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            var roots = GetContentRoots(treeOptions, globalOptions);
            foreach (var root in roots)
            {
                var r = ScriptPathFilters.Normalize(root.Trim()).Trim('/');
                if (r.Length == 0)
                {
                    continue;
                }

                var marker = "/" + r;
                if (!marker.EndsWith("/", StringComparison.Ordinal))
                {
                    marker += "/";
                }

                var idx = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                {
                    continue;
                }

                var after = normalized.Substring(idx + marker.Length);
                var parts = after.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length <= 1)
                {
                    folderSegments = Array.Empty<string>();
                    return true;
                }

                folderSegments = parts.Take(parts.Length - 1).ToArray();
                return true;
            }

            return false;
        }

        public static IReadOnlyList<string> GetRequiredSuffixSegments(
            IReadOnlyList<string> folderSegments,
            AnalyzerConfigOptions treeOptions,
            AnalyzerConfigOptions globalOptions)
        {
            if (folderSegments.Count == 0)
            {
                return Array.Empty<string>();
            }

            var segments = folderSegments.ToList();
            var legacyFirstSegment = !TryGetValuePresent(treeOptions, globalOptions, FirstSegmentIgnoreKey);
            var configured = legacyFirstSegment
                ? null
                : AnalyzerConfig.ParseSemicolonList(
                    TryGetRawFirstSegmentIgnore(treeOptions, globalOptions));

            if (legacyFirstSegment ||
                (configured != null && configured.Any(s => string.Equals(s, "*", StringComparison.Ordinal))))
            {
                if (segments.Count <= 1)
                {
                    return Array.Empty<string>();
                }

                segments.RemoveAt(0);
            }
            else if (configured != null && configured.Count > 0)
            {
                if (segments.Count > 0 &&
                    configured.Any(s => string.Equals(s, segments[0], StringComparison.Ordinal)))
                {
                    segments.RemoveAt(0);
                }
            }

            return segments.Where(segment => !IsSkippedNamespaceSegment(segment)).ToArray();
        }

        private static bool TryGetValuePresent(
            AnalyzerConfigOptions treeOptions,
            AnalyzerConfigOptions globalOptions,
            string key)
        {
            return treeOptions.TryGetValue(key, out _) || globalOptions.TryGetValue(key, out _);
        }

        private static string? TryGetRawFirstSegmentIgnore(
            AnalyzerConfigOptions treeOptions,
            AnalyzerConfigOptions globalOptions)
        {
            if (treeOptions.TryGetValue(FirstSegmentIgnoreKey, out var raw) && raw != null)
            {
                return raw;
            }

            return globalOptions.TryGetValue(FirstSegmentIgnoreKey, out raw) ? raw : null;
        }

        private static bool IsSkippedNamespaceSegment(string segment)
        {
            if (string.Equals(segment, "Runtime", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(segment, "Implementation", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// True if <paramref name="declaredNamespace"/> equals <c>R</c> or <c>R.suffix</c> for some allowed root <c>R</c>.
        /// </summary>
        public static bool MatchesDeclaredAgainstAllowedRootsAndSuffix(
            string declaredNamespace,
            IReadOnlyList<string> requiredSuffixSegments,
            HashSet<string> effectiveAllowedRoots)
        {
            if (string.IsNullOrWhiteSpace(declaredNamespace) || effectiveAllowedRoots.Count == 0)
            {
                return false;
            }

            var declaredSegments = declaredNamespace.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (declaredSegments.Length == 0)
            {
                return false;
            }

            foreach (var root in effectiveAllowedRoots)
            {
                if (requiredSuffixSegments.Count == 0)
                {
                    if (declaredSegments.Length == 1 &&
                        string.Equals(declaredSegments[0], root, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    continue;
                }

                if (declaredSegments.Length != requiredSuffixSegments.Count + 1)
                {
                    continue;
                }

                if (!string.Equals(declaredSegments[0], root, StringComparison.Ordinal))
                {
                    continue;
                }

                var ok = true;
                for (var i = 0; i < requiredSuffixSegments.Count; i++)
                {
                    if (!string.Equals(declaredSegments[i + 1], requiredSuffixSegments[i], StringComparison.Ordinal))
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// First segment of declared namespace, or empty.
        /// </summary>
        public static string GetFirstNamespaceSegment(string declaredNamespace)
        {
            if (string.IsNullOrWhiteSpace(declaredNamespace))
            {
                return string.Empty;
            }

            var trimmed = declaredNamespace.Trim();
            var dot = trimmed.IndexOf('.');
            return dot < 0 ? trimmed : trimmed.Substring(0, dot);
        }

        public static bool IsFirstSegmentAllowed(string declaredNamespace, HashSet<string> effectiveAllowedRoots)
        {
            var first = GetFirstNamespaceSegment(declaredNamespace);
            if (first.Length == 0)
            {
                return false;
            }

            return effectiveAllowedRoots.Contains(first);
        }

        /// <summary>
        /// Picks a root segment for displaying the expected full namespace in SCA3006 (deterministic).
        /// </summary>
        public static string PickRootSegmentForExpectedPath(
            HashSet<string> effectiveAllowedRoots,
            string? configuredRootSegment)
        {
            if (effectiveAllowedRoots.Count == 0)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(configuredRootSegment))
            {
                var seg = configuredRootSegment!.Trim();
                if (effectiveAllowedRoots.Contains(seg))
                {
                    return seg;
                }
            }

            return effectiveAllowedRoots.OrderBy(s => s, StringComparer.Ordinal).First();
        }

        public static string BuildFullNamespace(string rootSegment, IReadOnlyList<string> suffixSegments)
        {
            if (suffixSegments.Count == 0)
            {
                return rootSegment;
            }

            return rootSegment + "." + string.Join(".", suffixSegments);
        }

        public static bool IsPathIgnoredBySuffixGlobs(
            string normalizedPath,
            AnalyzerConfigOptions treeOptions,
            AnalyzerConfigOptions globalOptions)
        {
            if (!TryGetNonEmptyFromTreeOrGlobal(treeOptions, globalOptions, SuffixIgnoreGlobsKey, out var raw) ||
                string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            foreach (var pattern in AnalyzerConfig.ParseSemicolonList(raw))
            {
                var p = pattern.Trim();
                if (p.Length == 0)
                {
                    continue;
                }

                if (GlobMatch(normalizedPath, p))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Minimal glob: <c>**</c> at end = prefix match; otherwise path must equal glob (case-insensitive).
        /// </summary>
        private static bool GlobMatch(string normalizedPath, string glob)
        {
            var g = glob.Replace('\\', '/').Trim();
            if (g.Length == 0)
            {
                return false;
            }

            var p = normalizedPath.Replace('\\', '/');
            if (string.Equals(g, "**", StringComparison.Ordinal))
            {
                return true;
            }

            if (g.EndsWith("/**", StringComparison.Ordinal))
            {
                var prefix = g.Substring(0, g.Length - 3).TrimEnd('/');
                return HasPathPrefix(p, prefix);
            }

            return string.Equals(p, g, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasPathPrefix(string normalizedPath, string prefix)
        {
            var a = normalizedPath.Trim('/');
            var b = prefix.Trim('/');
            if (a.Length < b.Length)
            {
                return false;
            }

            if (!a.StartsWith(b, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return a.Length == b.Length || a[b.Length] == '/';
        }
    }
}
