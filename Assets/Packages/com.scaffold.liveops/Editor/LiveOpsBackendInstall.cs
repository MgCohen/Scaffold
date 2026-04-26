using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

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

        private static LiveOpsBackendInstallContext CreateContext(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot)) throw new InvalidOperationException("Project root is empty.");
            projectRoot = Path.GetFullPath(projectRoot);
            string packagesRoot = Path.Combine(projectRoot, "Assets", "Packages");
            if (!Directory.Exists(packagesRoot)) throw new InvalidOperationException("No packages under: " + packagesRoot);
            string hostBack = Path.Combine(packagesRoot, "com.scaffold.liveops", "Backend~");
            if (!Directory.Exists(hostBack)) throw new InvalidOperationException("Host Backend~ not found: " + hostBack + " (install com.scaffold.liveops with Backend~, or run refresh-liveops-template.ps1 in the Scaffold repo).");
            string destLive = Path.Combine(projectRoot, "LiveOps");
            return new LiveOpsBackendInstallContext(projectRoot, packagesRoot, destLive, hostBack);
        }

        private static void MergeAllPackages(LiveOpsBackendInstallContext ctx, bool dryRun)
        {
            var packageDirs = new List<string>(Directory.EnumerateDirectories(ctx.PackagesRoot));
            packageDirs.Sort(StringComparer.OrdinalIgnoreCase);
            bool syncedAny = false;
            foreach (string pkg in packageDirs)
            {
                syncedAny = TryMergeOnePackage(ctx, pkg, dryRun) || syncedAny;
            }
            if (!syncedAny) throw new InvalidOperationException("No Assets/Packages/*/Backend~ folders found (expected at least com.scaffold.liveops/Backend~).");
        }

        private static bool TryMergeOnePackage(LiveOpsBackendInstallContext ctx, string pkg, bool dryRun)
        {
            string back = Path.Combine(pkg, "Backend~");
            if (!Directory.Exists(back)) return false;
            string name = Path.GetFileName(pkg.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
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

        private static void EnsureGameDir(string destLive, bool dryRun)
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

        private static void ApplyHostTemplateFiles(LiveOpsBackendInstallContext ctx, bool dryRun)
        {
            CopyIfExistsDryRun(dryRun, Path.Combine(ctx.HostBack, "liveops.manifest.template.json"), Path.Combine(ctx.DestLive, "liveops.manifest.json"), "copy manifest");
            string installRecord = Path.Combine(ctx.DestLive, ".scaffold-install.json");
            if (dryRun)
            {
                UnityEngine.Debug.Log("[LiveOps] Would write: " + installRecord);
            }
            else
            {
                WriteInstallRecord(installRecord, ReadComScaffoldLiveopsVersion(ctx.ProjectRoot));
            }
            CopyIfExistsDryRun(dryRun, Path.Combine(ctx.HostBack, "LiveOps.Deploy.sln"), Path.Combine(ctx.DestLive, "LiveOps.Deploy.sln"), "copy solution");
        }

        private static void CopyIfExistsDryRun(bool dryRun, string from, string to, string label)
        {
            if (!File.Exists(from)) return;
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
            sb.Append("  \"notes\": \"").Append(EscapeJson("Merged from Assets/Packages/*/Backend~ (host: com.scaffold.liveops/Backend~)")).Append("\"\n");
            sb.Append("}\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        private static string ReadComScaffoldLiveopsVersion(string projectRoot)
        {
            string pkgPath = Path.Combine(projectRoot, "Assets", "Packages", "com.scaffold.liveops", "package.json");
            if (!File.Exists(pkgPath)) return "0.0.0";
            string text = File.ReadAllText(pkgPath);
            var m = Regex.Match(text, @"""version""\s*:\s*""([^""]+)""", RegexOptions.CultureInvariant);
            return m.Success ? m.Groups[1].Value : "0.0.0";
        }
    }
}
