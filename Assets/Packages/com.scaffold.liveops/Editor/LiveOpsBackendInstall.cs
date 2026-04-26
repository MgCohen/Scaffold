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
            string deploySln = Path.Combine(ctx.DestLive, "LiveOps.Deploy.sln");
            CopyRequiredHostTemplate(dryRun, Path.Combine(ctx.HostBack, "LiveOps.Deploy.sln"), deploySln, "copy solution");
            PruneMissingProjectsFromSolution(deploySln, dryRun);
            EnsureDiscoveredProjectsInSolution(ctx, deploySln, dryRun);
        }

        /// <summary>Sample: register on-disk LiveOps/Scaffold/** and LiveOps/Game/** csproj files in the deploy solution via <c>dotnet sln add</c>; warn-and-continue if dotnet is missing because the glob-based Deploy.targets still wires the build.</summary>
        private static void EnsureDiscoveredProjectsInSolution(LiveOpsBackendInstallContext ctx, string slnPath, bool dryRun)
        {
            if (dryRun) return;
            if (!File.Exists(slnPath)) return;
            List<string> discovered = DiscoverDeployFeatureProjects(ctx.DestLive);
            if (discovered.Count == 0) return;
            List<string> missing = FilterMissingFromSolution(discovered, slnPath);
            if (missing.Count == 0) return;
            TryRunDotnetSlnAdd(slnPath, missing);
        }

        private static List<string> FilterMissingFromSolution(List<string> discovered, string slnPath)
        {
            HashSet<string> existing = ReadSolutionProjectFullPaths(slnPath);
            var missing = new List<string>(discovered.Count);
            foreach (string csproj in discovered)
            {
                if (!existing.Contains(Path.GetFullPath(csproj))) missing.Add(csproj);
            }
            return missing;
        }

        private static void TryRunDotnetSlnAdd(string slnPath, List<string> missing)
        {
            try
            {
                RunDotnetSlnAdd(slnPath, missing);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[LiveOps] Skipped solution sync (dotnet sln add failed): " + ex.Message + ". Build still works via Scaffold.LiveOps.Deploy.targets globs.");
            }
        }

        private static List<string> DiscoverDeployFeatureProjects(string destLive)
        {
            var result = new List<string>();
            EnumerateDeployFeatureProjects(Path.Combine(destLive, "Scaffold"), result);
            EnumerateDeployFeatureProjects(Path.Combine(destLive, "Game"), result);
            return result;
        }

        private static void EnumerateDeployFeatureProjects(string root, List<string> sink)
        {
            if (!Directory.Exists(root)) return;
            foreach (string csproj in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
            {
                if (csproj.EndsWith(".Tests.csproj", StringComparison.OrdinalIgnoreCase)) continue;
                sink.Add(csproj);
            }
        }

        private static HashSet<string> ReadSolutionProjectFullPaths(string slnPath)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string slnDir = Path.GetDirectoryName(Path.GetFullPath(slnPath)) ?? string.Empty;
            var projectLine = new Regex("^Project\\(\"\\{[0-9A-Fa-f-]+\\}\"\\)\\s*=\\s*\"[^\"]*\",\\s*\"([^\"]+)\",\\s*\"\\{[0-9A-Fa-f-]+\\}\"\\s*$", RegexOptions.CultureInvariant);
            foreach (string line in File.ReadAllLines(slnPath))
            {
                AddProjectFullPathFromLine(line, slnDir, projectLine, set);
            }
            return set;
        }

        private static void AddProjectFullPathFromLine(string line, string slnDir, Regex projectLine, HashSet<string> set)
        {
            Match m = projectLine.Match(line);
            if (!m.Success) return;
            string rel = m.Groups[1].Value;
            if (!rel.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) return;
            string normalized = rel.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            string combined = Path.Combine(slnDir, normalized);
            set.Add(Path.GetFullPath(combined));
        }

        private static void RunDotnetSlnAdd(string slnPath, List<string> csprojPaths)
        {
            var startInfo = BuildDotnetSlnAddStartInfo(slnPath, csprojPaths);
            using var proc = Process.Start(startInfo);
            if (proc is null) throw new InvalidOperationException("Failed to start dotnet process.");
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            if (proc.ExitCode != 0) throw new InvalidOperationException("dotnet sln add exit " + proc.ExitCode + ": " + stderr.Trim() + " " + stdout.Trim());
            UnityEngine.Debug.Log("[LiveOps] Synced " + csprojPaths.Count + " feature/game project(s) into " + slnPath);
        }

        private static ProcessStartInfo BuildDotnetSlnAddStartInfo(string slnPath, List<string> csprojPaths)
        {
            string arguments = BuildDotnetSlnAddArgumentsLine(slnPath, csprojPaths);
            return new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
        }

        private static string BuildDotnetSlnAddArgumentsLine(string slnPath, List<string> csprojPaths)
        {
            var sb = new StringBuilder(256);
            sb.Append("sln ");
            sb.Append(QuoteForCmd(slnPath));
            sb.Append(" add");
            foreach (string csproj in csprojPaths)
            {
                sb.Append(' ');
                sb.Append(QuoteForCmd(csproj));
            }
            return sb.ToString();
        }

        /// <summary>Sample: strip <c>Project</c> entries from <paramref name="slnPath"/> whose <c>.csproj</c> is absent (peer LiveOps packages opted out), so <c>ugs deploy</c> / <c>dotnet build</c> don't fail with MSB3202.</summary>
        private static void PruneMissingProjectsFromSolution(string slnPath, bool dryRun)
        {
            if (dryRun) return;
            if (!File.Exists(slnPath)) return;
            string[] lines = File.ReadAllLines(slnPath);
            var projectLine = new Regex("^Project\\(\"\\{[0-9A-Fa-f-]+\\}\"\\)\\s*=\\s*\"[^\"]*\",\\s*\"([^\"]+)\",\\s*\"(\\{[0-9A-Fa-f-]+\\})\"\\s*$", RegexOptions.CultureInvariant);
            HashSet<string> missingGuids = CollectMissingProjectGuids(lines, slnPath, projectLine);
            if (missingGuids.Count == 0) return;
            List<string> output = StripProjectEntriesByGuid(lines, missingGuids, projectLine);
            File.WriteAllLines(slnPath, output, new UTF8Encoding(false));
            UnityEngine.Debug.Log("[LiveOps] Pruned " + missingGuids.Count + " missing project reference(s) from " + slnPath);
        }

        private static HashSet<string> CollectMissingProjectGuids(string[] lines, string slnPath, Regex projectLine)
        {
            string slnDir = Path.GetDirectoryName(Path.GetFullPath(slnPath)) ?? string.Empty;
            var missingGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in lines)
            {
                AddIfMissingProjectGuid(line, slnDir, projectLine, missingGuids);
            }
            return missingGuids;
        }

        private static void AddIfMissingProjectGuid(string line, string slnDir, Regex projectLine, HashSet<string> missingGuids)
        {
            Match m = projectLine.Match(line);
            if (!m.Success) return;
            string rel = m.Groups[1].Value;
            if (!rel.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) return;
            string normalized = rel.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            string combined = Path.Combine(slnDir, normalized);
            string full = Path.GetFullPath(combined);
            if (!File.Exists(full)) missingGuids.Add(m.Groups[2].Value);
        }

        private static List<string> StripProjectEntriesByGuid(string[] lines, HashSet<string> missingGuids, Regex projectLine)
        {
            var output = new List<string>(lines.Length);
            bool dropping = false;
            foreach (string raw in lines)
            {
                dropping = ProcessSlnLineForStrip(raw, missingGuids, projectLine, dropping, output);
            }
            return output;
        }

        private static bool ProcessSlnLineForStrip(string raw, HashSet<string> missingGuids, Regex projectLine, bool dropping, List<string> output)
        {
            if (dropping)
            {
                if (raw.TrimStart().StartsWith("EndProject", StringComparison.Ordinal)) return false;
                return true;
            }
            Match m = projectLine.Match(raw);
            if (m.Success && missingGuids.Contains(m.Groups[2].Value)) return true;
            if (LineStartsWithAnyGuid(raw, missingGuids)) return false;
            output.Add(raw);
            return false;
        }

        private static bool LineStartsWithAnyGuid(string raw, HashSet<string> missingGuids)
        {
            string trimmed = raw.TrimStart();
            foreach (string g in missingGuids)
            {
                if (trimmed.StartsWith(g, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>Sample: copy a required host template (deploy solution or manifest) from <paramref name="from"/> to <paramref name="to"/>; throw a clear error when the source is missing (typical for UPM Git pulls that gitignore <c>Backend~</c> .sln/.csproj).</summary>
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
