using System;
using System.IO;

namespace Scaffold.Analyzers
{
    /// <summary>
    /// Shared path normalization and Unity script location predicates. New rules must use this instead of duplicating path checks.
    /// </summary>
    internal static class ScriptPathFilters
    {
        public static string Normalize(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path!.Replace('\\', '/');
        }

        /// <summary>
        /// File name segment after the last slash (works for synthetic Windows paths under non-Windows runtimes where <see cref="System.IO.Path.GetFileName"/> does not treat '\' as a separator).
        /// </summary>
        public static string GetFileName(string? filePath)
        {
            var n = Normalize(filePath);
            if (string.IsNullOrWhiteSpace(n))
            {
                return string.Empty;
            }

            var lastSlash = n.LastIndexOf('/');
            return lastSlash >= 0 ? n.Substring(lastSlash + 1) : n;
        }

        /// <summary>
        /// Extension-stripped file name from <see cref="GetFileName"/> (e.g. <c>Game.cs</c> → <c>Game</c>).
        /// </summary>
        public static string GetFileNameWithoutExtension(string? filePath)
        {
            var name = GetFileName(filePath);
            return string.IsNullOrEmpty(name) ? string.Empty : Path.GetFileNameWithoutExtension(name);
        }

        /// <summary>
        /// Unity often supplies <c>Assets/Scripts/...</c> without a leading slash; MSBuild may use <c>.../Assets/Scripts/...</c>.
        /// </summary>
        public static bool TryGetPathAfterAssetsScripts(string normalized, out string remainder)
        {
            remainder = string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            const string token = "Assets/Scripts/";
            int remainderStart;
            if (normalized.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            {
                remainderStart = token.Length;
            }
            else
            {
                var idx = normalized.IndexOf("/" + token, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                {
                    return false;
                }

                remainderStart = idx + 1 + token.Length;
            }

            remainder = remainderStart <= normalized.Length ? normalized.Substring(remainderStart) : string.Empty;
            return true;
        }

        public static bool IsUnderAssetsScripts(string normalized)
        {
            return TryGetPathAfterAssetsScripts(normalized, out _);
        }

        public static bool IsTestOrSamplePath(string normalized)
        {
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            return normalized.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Samples/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// obj/bin paths and *.g.cs — common build artifact paths for Unity script analysis.
        /// </summary>
        public static bool IsGeneratedOrBuildArtifactPath(string normalized)
        {
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            return normalized.IndexOf("/obj/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Source generators, designers, and related patterns.
        /// </summary>
        public static bool IsGeneratedSourceFilePattern(string normalized)
        {
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (IsGeneratedOrBuildArtifactPath(normalized))
            {
                return true;
            }

            if (normalized.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normalized.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normalized.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Project-owned Unity scripts under Assets/Scripts; excludes tests, samples, and build/generated artifacts used by placement rules.
        /// </summary>
        public static bool IsUnityScriptPath(string? filePath)
        {
            var n = Normalize(filePath);
            if (!IsUnderAssetsScripts(n))
            {
                return false;
            }

            if (IsTestOrSamplePath(n))
            {
                return false;
            }

            if (IsGeneratedOrBuildArtifactPath(n))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Scripts under Assets/Scripts with a /Runtime/ segment; excludes tests, samples, and build artifacts (SCA8002).
        /// </summary>
        public static bool IsRuntimeUnityScriptPath(string? filePath)
        {
            var n = Normalize(filePath);
            if (!IsUnderAssetsScripts(n))
            {
                return false;
            }

            if (n.IndexOf("/Runtime/", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (IsTestOrSamplePath(n))
            {
                return false;
            }

            if (IsGeneratedOrBuildArtifactPath(n))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// SCA8001: trees that may contain dead-code candidates (/Runtime/ under Assets/Scripts, excluding tests/samples/generated).
        /// </summary>
        public static bool IsSca0030RuntimeCandidatePath(string? filePath)
        {
            var n = Normalize(filePath);
            if (n.IndexOf("/Runtime/", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (!IsUnderAssetsScripts(n))
            {
                return false;
            }

            if (IsGeneratedOrBuildArtifactPath(n))
            {
                return false;
            }

            if (IsTestOrSamplePath(n))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Trees excluded when scanning for cross-assembly references (tests, samples, obj/bin, *.g.cs).
        /// </summary>
        public static bool IsTestSampleOrGeneratedPath(string? filePath)
        {
            var n = Normalize(filePath);
            return IsTestOrSamplePath(n) || IsGeneratedOrBuildArtifactPath(n);
        }
    }
}
