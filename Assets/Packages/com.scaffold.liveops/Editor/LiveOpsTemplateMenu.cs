using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Scaffold.LiveOps.Editor
{
    /// <summary>
    /// Runs the same <c>refresh-liveops-template.ps1</c> / <c>install-liveops-backend.ps1</c> as the CLI.
    /// MSBuild also runs <c>refresh-liveops-template.ps1 -SkipGeneratorBuild</c> after <c>LiveOps</c> and
    /// <c>Scaffold.LiveOps.Bootstrap.Generators</c> when <c>ScaffoldSyncLiveOpsTemplateOnBuild</c> is true (see
    /// <c>LiveOps/Deploy/Build/Scaffold.LiveOps.TemplateSync.targets</c>).
    /// </summary>
    public static class LiveOpsTemplateMenu
    {
        private const string InstallMenu = "Scaffold/LiveOps/Install or Update Backend (from package Template~)";
        private const string RefreshMenu = "Scaffold/LiveOps/Refresh Backend Template (sync Template~ from LiveOps/)";

        [MenuItem(InstallMenu)]
        public static void InstallOrUpdateBackend()
        {
            RunAgentScript("install-liveops-backend.ps1");
        }

        [MenuItem(RefreshMenu)]
        public static void RefreshBackendTemplate()
        {
            RunAgentScript("refresh-liveops-template.ps1");
        }

        private static void RunAgentScript(string fileName)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string script = Path.GetFullPath(Path.Combine(projectRoot, ".agents", "scripts", fileName));
            if (!File.Exists(script))
            {
                Debug.LogError($"[LiveOps] Script not found: {script}. Copy the Unity project root that contains the Scaffold repo and .agents/scripts.");
                return;
            }

            string pwsh = FindPwsh();
            if (string.IsNullOrEmpty(pwsh))
            {
                Debug.LogWarning(
                    "[LiveOps] PowerShell 7+ (pwsh) not on PATH. Run this from the project root: " +
                    $"pwsh -NoProfile -File \"{script}\"");
                return;
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = pwsh,
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"",
                UseShellExecute = false,
                WorkingDirectory = projectRoot,
                CreateNoWindow = false,
            };

            using System.Diagnostics.Process? p = System.Diagnostics.Process.Start(psi);
            if (p is null)
            {
                Debug.LogError("[LiveOps] Could not start pwsh process.");
                return;
            }

            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                Debug.LogError($"[LiveOps] {fileName} exited with code {p.ExitCode}.");
            }
            else
            {
                Debug.Log($"[LiveOps] {fileName} completed successfully.");
                AssetDatabase.Refresh();
            }
        }

        private static string? FindPwsh()
        {
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
            {
                return null;
            }

            foreach (string? segment in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrEmpty(segment))
                {
                    continue;
                }

                string candidate = Path.Combine(segment, "pwsh" + (Environment.OSVersion.Platform == PlatformID.Win32NT ? ".exe" : ""));
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
