using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor.PackageManager;

namespace Scaffold.LiveOps.Editor
{
    /// <summary>Sample: in-package merge of Backend~ trees into LiveOps/; parity with .agents install-liveops-backend.ps1 for CLI/CI in Scaffold.</summary>
    internal static class LiveOpsBackendInstall
    {
        private static readonly string[] robocopyExcludeDirNames = { "bin", "obj", ".vs", ".artifacts" };

        /// <summary>Sample: orchestrates merge and host template steps; see class summary on LiveOpsBackendInstall.</summary>
        public static void Run(string projectRoot, bool dryRun)
        {
            LiveOpsBackendInstallContext ctx = CreateContext(projectRoot);
            MergeAllPackages(ctx, dryRun);
            EnsureGameDir(ctx.DestLive, dryRun);
            ApplyHostTemplateFiles(ctx, dryRun);
            LogPostInstallHints(ctx.DestLive, dryRun);
        }

        internal static LiveOpsBackendInstallContext CreateContext(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot)) throw new InvalidOperationException("Project root is empty.");
            projectRoot = Path.GetFullPath(projectRoot);
            string hostBack = ResolveHostBackendDirectory(projectRoot);
            string destLive = Path.Combine(projectRoot, "LiveOps");
            return new LiveOpsBackendInstallContext(projectRoot, destLive, hostBack);
        }

        /// <summary>Sample: merges a single <c>packageDir/Backend~</c> into <see cref="LiveOpsBackendInstallContext.DestLive"/>. Skips and logs if <c>Backend~</c> is missing (returns <c>false</c>).</summary>
        internal static bool MergeOnePackage(LiveOpsBackendInstallContext ctx, string packageDir, bool dryRun)
        {
            string back = Path.Combine(Path.GetFullPath(packageDir), "Backend~");
            if (!Directory.Exists(back))
            {
                UnityEngine.Debug.Log("[LiveOps] No Backend~ under " + packageDir + " — skipped.");
                return false;
            }

            string name = Path.GetFileName(packageDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return MergeFromBackendPath(ctx, Path.GetFullPath(back), name, dryRun);
        }

        private static string ResolveHostBackendDirectory(string projectRoot)
        {
            string embedded = Path.Combine(projectRoot, "Assets", "Packages", "com.scaffold.liveops", "Backend~");
            if (Directory.Exists(embedded)) return Path.GetFullPath(embedded);

            PackageInfo? info = PackageInfo.FindForAssembly(typeof(LiveOpsBackendInstall).Assembly);
            if (info is not null && !string.IsNullOrEmpty(info.resolvedPath))
            {
                string fromUpm = Path.Combine(info.resolvedPath, "Backend~");
                if (Directory.Exists(fromUpm)) return Path.GetFullPath(fromUpm);
            }

            throw new InvalidOperationException(
                "Host Backend~ not found. Expected Assets/Packages/com.scaffold.liveops/Backend~ or the same under the UPM package path (e.g. Library/PackageCache/.../Backend~). " +
                "Tried: " + embedded);
        }

        private static void MergeAllPackages(LiveOpsBackendInstallContext ctx, bool dryRun)
        {
            List<(string BackPath, string Label)> entries = CollectBackendMergeEntries(ctx.ProjectRoot);
            if (entries.Count == 0) throw new InvalidOperationException("No Backend~ folders found under Assets/Packages or UPM packages (expected at least com.scaffold.liveops/Backend~).");
            bool syncedAny = false;
            foreach ((string backPath, string label) in entries)
            {
                syncedAny = MergeFromBackendPath(ctx, backPath, label, dryRun) || syncedAny;
            }
            if (!syncedAny) throw new InvalidOperationException("No package Backend~ folders were merged (unexpected).");
        }

        private static List<(string BackPath, string Label)> CollectBackendMergeEntries(string projectRoot)
        {
            var result = new List<(string, string)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddBackendsFromAssetsPackages(projectRoot, result, seen);
            AddBackendsFromRegisteredPackages(result, seen);
            result.Sort((a, b) => string.Compare(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private static void AddBackendsFromAssetsPackages(string projectRoot, List<(string full, string label)> result, HashSet<string> seen)
        {
            string assetsPackages = Path.Combine(projectRoot, "Assets", "Packages");
            if (!Directory.Exists(assetsPackages)) return;
            foreach (string d in Directory.EnumerateDirectories(assetsPackages))
            {
                TryAddBackend(result, seen, Path.Combine(d, "Backend~"), Path.GetFileName(d));
            }
        }

        private static void AddBackendsFromRegisteredPackages(List<(string full, string label)> result, HashSet<string> seen)
        {
            foreach (PackageInfo pi in PackageInfo.GetAllRegisteredPackages())
            {
                if (string.IsNullOrEmpty(pi.resolvedPath)) continue;
                TryAddBackend(result, seen, Path.Combine(pi.resolvedPath, "Backend~"), pi.name);
            }
        }

        private static void TryAddBackend(List<(string full, string label)> result, HashSet<string> seen, string backPath, string label)
        {
            if (!Directory.Exists(backPath)) return;
            string full = Path.GetFullPath(backPath);
            if (!seen.Add(full)) return;
            result.Add((full, label));
        }

        private static bool MergeFromBackendPath(LiveOpsBackendInstallContext ctx, string back, string name, bool dryRun)
        {
            UnityEngine.Debug.Log("[LiveOps] Merge Backend~ from " + name + " -> " + ctx.DestLive + " [" + (dryRun ? "dry-run" : "apply") + "]");
            if (dryRun) return true;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RunRobocopyMerge(back, ctx.DestLive, name);
            }
            else
            {
                RunManagedMerge(back, ctx.DestLive);
            }
            return true;
        }

        internal static void EnsureGameDir(string destLive, bool dryRun)
        {
            string game = Path.Combine(destLive, "Game");
            if (Directory.Exists(game)) return;
            if (dryRun)
            {
                UnityEngine.Debug.Log("[LiveOps] Would create: " + game);
            }
            else
            {
                Directory.CreateDirectory(game);
            }
        }

        internal static void ApplyHostTemplateFiles(LiveOpsBackendInstallContext ctx, bool dryRun)
        {
            CopyRequiredHostTemplate(dryRun, Path.Combine(ctx.HostBack, "liveops.manifest.template.json"), Path.Combine(ctx.DestLive, "liveops.manifest.json"), "copy manifest");
            string installRecord = Path.Combine(ctx.DestLive, ".scaffold-install.json");
            if (dryRun)
            {
                UnityEngine.Debug.Log("[LiveOps] Would write: " + installRecord);
            }
            else
            {
                WriteInstallRecord(installRecord, ReadComScaffoldLiveopsVersion(ctx.HostBack));
            }
            CopyRequiredHostTemplate(dryRun, Path.Combine(ctx.HostBack, "LiveOps.Deploy.sln"), Path.Combine(ctx.DestLive, "LiveOps.Deploy.sln"), "copy solution");
        }

        // Host template files (Deploy solution + manifest) are required. Surface a clear error when the
        // resolved package is missing them — usually a UPM Git pull where .sln/.csproj are gitignored.
        private static void CopyRequiredHostTemplate(bool dryRun, string from, string to, string label)
        {
            if (!File.Exists(from))
            {
                throw new FileNotFoundException(
                    "Host template missing: " + from + ". " +
                    "If com.scaffold.liveops was installed via UPM Git, ensure the source repo's .gitignore does not ignore " +
                    "Assets/Packages/**/Backend~/**/*.sln and *.csproj, and that the file is committed.", from);
            }
            if (dryRun)
            {
                UnityEngine.Debug.Log("[LiveOps] Would " + label + " " + from + " -> " + to);
            }
            else
            {
                File.Copy(from, to, true);
            }
        }

        private static void LogPostInstallHints(string destLive, bool dryRun)
        {
            UnityEngine.Debug.Log("[LiveOps] Game tree preserved. Cloud Code (.ccmr): use " + Path.Combine(destLive, "LiveOps.Deploy.sln") + " (not LiveOps.sln, which includes tests and can exceed the 10MB upload cap).");
            if (dryRun) return;
            string csproj = Path.Combine(destLive, "Deploy", "LiveOps", "LiveOps.csproj");
            UnityEngine.Debug.Log("[LiveOps] Optional: dotnet build \"" + csproj + "\" -c Release");
        }

        private static void RunRobocopyMerge(string back, string destLive, string packageName)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "robocopy",
                Arguments = BuildRobocopyArgumentsLine(back, destLive),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            using var p = Process.Start(startInfo);
            if (p is null) throw new InvalidOperationException("Failed to start robocopy for package: " + packageName);
            p.WaitForExit();
            if (p.ExitCode >= 8) throw new InvalidOperationException("robocopy failed with exit code " + p.ExitCode + " for package " + packageName);
        }

        private static string BuildRobocopyArgumentsLine(string back, string destLive)
        {
            var sb = new StringBuilder(256);
            sb.Append(QuoteForCmd(back));
            sb.Append(' ');
            sb.Append(QuoteForCmd(destLive));
            sb.Append(" /E /NFL /NDL /NJH /NJS /R:3 /W:1");
            foreach (string d in robocopyExcludeDirNames)
            {
                sb.Append(" /xd ");
                sb.Append(QuoteForCmd(d));
            }
            return sb.ToString();
        }

        private static string QuoteForCmd(string a)
        {
            if (string.IsNullOrEmpty(a)) return "\"\"";
            a = a.Replace("\"", "\"\"", StringComparison.Ordinal);
            return "\"" + a + "\"";
        }

        private static void RunManagedMerge(string back, string destLive)
        {
            string backFull = Path.GetFullPath(back);
            string destFull = Path.GetFullPath(destLive);
            var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string d in robocopyExcludeDirNames)
            {
                exclude.Add(d);
            }
            foreach (string file in Directory.EnumerateFiles(backFull, "*", SearchOption.AllDirectories))
            {
                CopyIfNotExcluded(file, backFull, destFull, exclude);
            }
        }

        private static void CopyIfNotExcluded(string file, string backFull, string destFull, HashSet<string> exclude)
        {
            if (PathIsUnderExcludedDir(backFull, file, exclude)) return;
            string rel = Path.GetRelativePath(backFull, file);
            string outFile = Path.Combine(destFull, rel);
            string? outDir = Path.GetDirectoryName(outFile);
            if (outDir is not null) Directory.CreateDirectory(outDir);
            File.Copy(file, outFile, true);
        }

        private static bool PathIsUnderExcludedDir(string backFull, string filePath, HashSet<string> excludeDirNames)
        {
            string rel = Path.GetRelativePath(backFull, filePath);
            var parts = rel.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (excludeDirNames.Contains(parts[i])) return true;
            }
            return false;
        }

        private static void WriteInstallRecord(string path, string templateVersion)
        {
            string iso = DateTime.UtcNow.ToString("o");
            var sb = new StringBuilder(400);
            sb.Append("{\n");
            sb.Append("  \"templateVersion\": \"").Append(EscapeJson(templateVersion)).Append("\",\n");
            sb.Append("  \"installedRoots\": [\n");
            sb.Append("    \"LiveOps/Deploy\",\n");
            sb.Append("    \"LiveOps/Scaffold\"\n");
            sb.Append("  ],\n");
            sb.Append("  \"lastUpdate\": \"").Append(EscapeJson(iso)).Append("\",\n");
            sb.Append("  \"notes\": \"").Append(EscapeJson("Merged from package Backend~ (Assets/Packages and UPM; host: com.scaffold.liveops)")).Append("\"\n");
            sb.Append("}\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        private static string ReadComScaffoldLiveopsVersion(string hostBackDirectory)
        {
            string? packageRoot = Path.GetDirectoryName(hostBackDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(packageRoot)) return "0.0.0";
            string pkgPath = Path.Combine(packageRoot, "package.json");
            if (!File.Exists(pkgPath)) return "0.0.0";
            string text = File.ReadAllText(pkgPath);
            var m = Regex.Match(text, @"""version""\s*:\s*""([^""]+)""", RegexOptions.CultureInvariant);
            return m.Success ? m.Groups[1].Value : "0.0.0";
        }
    }
}
