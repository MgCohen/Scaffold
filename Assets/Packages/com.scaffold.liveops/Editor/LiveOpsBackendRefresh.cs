using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace Scaffold.LiveOps.Editor
{
    /// <summary>Sample: in-editor port of <c>refresh-liveops-template.ps1</c> (no generator build).</summary>
    internal static class LiveOpsBackendRefresh
    {
        private static readonly string[] robocopyExcludeDirNames = { "bin", "obj", ".vs", ".artifacts" };
        private static readonly string[] topLevelSyncFolders = { "Deploy", "Scaffold" };

        /// <summary>Sample: syncs every <c>Assets/Packages/*/Backend~</c> and host template files.</summary>
        internal static void RunAll(string projectRoot, bool dryRun)
        {
            if (string.IsNullOrEmpty(projectRoot))
            {
                return;
            }
            RunAllForRoot(Path.GetFullPath(projectRoot), dryRun);
        }

        private static void RunAllForRoot(string root, bool dryRun)
        {
            LiveOpsBackendInstallContext ctx = LiveOpsBackendInstall.CreateContext(root);
            string packagesRoot = Path.Combine(root, "Assets", "Packages");
            if (!Directory.Exists(packagesRoot))
            {
                Debug.LogWarning("[LiveOps] Refresh: no Assets/Packages — skipped.");
                return;
            }
            RunRefreshOverPackages(packagesRoot, ctx, dryRun);
        }

        private static void RunRefreshOverPackages(string packagesRoot, LiveOpsBackendInstallContext ctx, bool dryRun)
        {
            foreach (string pkg in Directory.EnumerateDirectories(packagesRoot))
            {
                RefreshOnePackage(ctx, pkg, dryRun);
            }
        }

        /// <summary>Sample: syncs <c>LiveOps/{Deploy,Scaffold}/&lt;module&gt;</c> into one package <c>Backend~</c>.</summary>
        internal static bool RefreshOnePackage(LiveOpsBackendInstallContext ctx, string packageDir, bool dryRun)
        {
            string back = Path.Combine(Path.GetFullPath(packageDir), "Backend~");
            if (!Directory.Exists(back))
            {
                return false;
            }
            bool any = SyncAllTopLevelModules(ctx, back, packageDir, dryRun);
            if (IsHostPackage(packageDir))
            {
                CopyHostTemplateFromLiveOps(ctx, dryRun);
            }
            return any || IsHostPackage(packageDir);
        }

        private static bool SyncAllTopLevelModules(LiveOpsBackendInstallContext ctx, string back, string packageDir, bool dryRun)
        {
            string liveOps = ctx.DestLive;
            string pkgName = Path.GetFileName(packageDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            bool any = false;
            foreach (string topName in topLevelSyncFolders)
            {
                any = SyncOneTopWork(back, liveOps, pkgName, topName, dryRun) || any;
            }
            return any;
        }

        private static bool SyncOneTopWork(string back, string liveOps, string pkgName, string topName, bool dryRun)
        {
            string backTop = Path.Combine(back, topName);
            if (!Directory.Exists(backTop))
            {
                return false;
            }
            string liveTop = Path.Combine(liveOps, topName);
            if (!Directory.Exists(liveTop))
            {
                LogMissingLiveTop(liveTop, pkgName);
                return false;
            }
            return AccumulateModuleDirectorySyncs(liveTop, backTop, pkgName, topName, dryRun);
        }

        private static void LogMissingLiveTop(string liveTop, string pkgName)
        {
            Debug.LogWarning("[LiveOps] Refresh: LiveOps side missing, skip: " + liveTop + " (package " + pkgName + ")");
        }

        private static bool AccumulateModuleDirectorySyncs(string liveTop, string backTop, string pkgName, string topName, bool dryRun)
        {
            bool any = false;
            foreach (string modDir in Directory.EnumerateDirectories(backTop))
            {
                any = TrySyncOneModule(liveTop, modDir, pkgName, topName, dryRun) || any;
            }
            return any;
        }

        private static bool TrySyncOneModule(string liveTop, string modDir, string pkgName, string topName, bool dryRun)
        {
            string modName = Path.GetFileName(modDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string src = Path.Combine(liveTop, modName);
            if (!Directory.Exists(src))
            {
                Debug.LogWarning("[LiveOps] Refresh: no source in repo (skip): " + src);
                return false;
            }
            string dst = modDir;
            string label = pkgName + "/" + topName + "/" + modName;
            DoMirrorOrLog(src, dst, label, dryRun);
            return true;
        }

        private static void DoMirrorOrLog(string src, string dst, string label, bool dryRun)
        {
            Debug.Log("[LiveOps] Sync " + src + " -> " + dst + (dryRun ? " [dry-run]" : string.Empty));
            if (dryRun)
            {
                return;
            }
            Directory.CreateDirectory(dst);
            MirrorOneModule(src, dst, label);
        }

        internal static bool IsHostPackage(string packageDir)
        {
            return string.Equals(PackageFolderName(packageDir), "com.scaffold.liveops", StringComparison.OrdinalIgnoreCase);
        }

        private static string PackageFolderName(string packageDir)
        {
            return Path.GetFileName(Path.GetFullPath(packageDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        private static void CopyHostTemplateFromLiveOps(LiveOpsBackendInstallContext ctx, bool dryRun)
        {
            CopyHostSlnIfPresent(ctx, dryRun);
        }

        private static void CopyHostSlnIfPresent(LiveOpsBackendInstallContext ctx, bool dryRun)
        {
            string src = Path.Combine(ctx.DestLive, "LiveOps.Deploy.sln");
            string dst = Path.Combine(ctx.HostBack, "LiveOps.Deploy.sln");
            if (!File.Exists(src))
            {
                return;
            }
            if (dryRun)
            {
                Debug.Log("[LiveOps] Would copy " + src + " -> " + dst);
                return;
            }
            File.Copy(src, dst, true);
        }

        private static void MirrorOneModule(string src, string dst, string labelForErrors)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RunRobocopyMirror(src, dst, labelForErrors);
            }
            else
            {
                MirrorManagedReplace(src, dst);
            }
        }

        private static void RunRobocopyMirror(string src, string dst, string labelForErrors)
        {
            var startInfo = new ProcessStartInfo { FileName = "robocopy", Arguments = BuildRobocopyMirrorLine(src, dst), UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, };
            using Process? p = Process.Start(startInfo);
            if (p is null)
            {
                throw new InvalidOperationException("Failed to start robocopy for: " + labelForErrors);
            }
            p.WaitForExit();
            if (p.ExitCode >= 8)
            {
                throw new InvalidOperationException("robocopy failed with exit code " + p.ExitCode + " for " + labelForErrors);
            }
        }

        private static string BuildRobocopyMirrorLine(string src, string dst)
        {
            var sb = new StringBuilder(256);
            sb.Append(QuoteForCmd(src));
            sb.Append(' ');
            sb.Append(QuoteForCmd(dst));
            sb.Append(" /MIR /NFL /NDL /NJH /NJS /R:3 /W:1");
            foreach (string d in robocopyExcludeDirNames)
            {
                sb.Append(" /xd ");
                sb.Append(QuoteForCmd(d));
            }
            return sb.ToString();
        }

        private static string QuoteForCmd(string a)
        {
            if (string.IsNullOrEmpty(a))
            {
                return "\"\"";
            }
            string x = a.Replace("\"", "\"\"", StringComparison.Ordinal);
            return "\"" + x + "\"";
        }

        private static void MirrorManagedReplace(string src, string dst)
        {
            MirrorManagedNonWindows(src, dst);
        }

        private static void MirrorManagedNonWindows(string src, string dst)
        {
            string srcFull = Path.GetFullPath(src);
            string dstFull = Path.GetFullPath(dst);
            HashSet<string> exclude = BuildExcludeSet();
            ResetDestinationTree(dstFull);
            CopySourceTreeExceptExcluded(srcFull, dstFull, exclude);
        }

        private static HashSet<string> BuildExcludeSet()
        {
            var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string d in robocopyExcludeDirNames)
            {
                exclude.Add(d);
            }
            return exclude;
        }

        private static void ResetDestinationTree(string dstFull)
        {
            if (Directory.Exists(dstFull))
            {
                Directory.Delete(dstFull, true);
            }
            Directory.CreateDirectory(dstFull);
        }

        private static void CopySourceTreeExceptExcluded(string srcFull, string dstFull, HashSet<string> exclude)
        {
            foreach (string file in Directory.EnumerateFiles(srcFull, "*", SearchOption.AllDirectories))
            {
                CopyOneFileIfAllowed(srcFull, dstFull, file, exclude);
            }
        }

        private static void CopyOneFileIfAllowed(string srcFull, string dstFull, string file, HashSet<string> exclude)
        {
            if (PathIsUnderExcludedDir(srcFull, file, exclude))
            {
                return;
            }
            string rel = Path.GetRelativePath(srcFull, file);
            string outFile = Path.Combine(dstFull, rel);
            string? outDir = Path.GetDirectoryName(outFile);
            if (outDir is not null)
            {
                Directory.CreateDirectory(outDir);
            }
            File.Copy(file, outFile, true);
        }

        private static bool PathIsUnderExcludedDir(string rootFull, string filePath, HashSet<string> excludeDirNames)
        {
            string rel = Path.GetRelativePath(rootFull, filePath);
            var parts = rel.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (excludeDirNames.Contains(parts[i]))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
