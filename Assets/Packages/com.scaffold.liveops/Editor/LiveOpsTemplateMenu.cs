using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Scaffold.LiveOps.Editor
{
    /// <summary>Sample menu: runs <c>install-liveops-backend.ps1</c> / <c>refresh-liveops-template.ps1</c> (see <c>SCAFFOLD_LIVEOPS_PACKAGE_DEV</c>).</summary>
    public static class LiveOpsTemplateMenu
    {
        private const string installMenu = "Scaffold/LiveOps/Install or Update Backend";

        [MenuItem(installMenu)]
        public static void InstallOrUpdateBackend()
        {
            RunAgentScript("install-liveops-backend.ps1");
        }

#if SCAFFOLD_LIVEOPS_PACKAGE_DEV
        private const string refreshMenu = "Scaffold/LiveOps/Refresh Backend Template";

        [MenuItem(refreshMenu)]
        public static void RefreshBackendTemplate()
        {
            RunAgentScript("refresh-liveops-template.ps1");
        }
#endif

        private static void RunAgentScript(string fileName)
        {
            if (!TryGetAgentScriptPath(fileName, out string root, out string script)) return;
            string? pwsh = FindPwsh();
            if (string.IsNullOrEmpty(pwsh))
            {
                Debug.LogWarning("[LiveOps] PowerShell 7+ (pwsh) not on PATH. Run: pwsh -NoProfile -File \"" + script + "\"");
                return;
            }

            RunPwshWithScript(root, pwsh, script, fileName);
        }

        private static bool TryGetAgentScriptPath(string fileName, out string projectRoot, out string scriptPath)
        {
            projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            scriptPath = Path.GetFullPath(Path.Combine(projectRoot, ".agents", "scripts", fileName));
            if (File.Exists(scriptPath)) return true;
            Debug.LogError($"[LiveOps] Script not found: {scriptPath}. Copy the Unity project root that contains the Scaffold repo and .agents/scripts.");
            return false;
        }

        private static void RunPwshWithScript(string projectRoot, string pwsh, string script, string fileName)
        {
            using System.Diagnostics.Process? p = System.Diagnostics.Process.Start(CreatePwshStartInfo(projectRoot, pwsh, script));
            if (p is null)
            {
                Debug.LogError("[LiveOps] Could not start pwsh process.");
                return;
            }

            p.WaitForExit();
            LogPwshExit(fileName, p.ExitCode);
        }

        private static System.Diagnostics.ProcessStartInfo CreatePwshStartInfo(string projectRoot, string pwsh, string script)
        {
            return new System.Diagnostics.ProcessStartInfo
            {
                FileName = pwsh,
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"",
                UseShellExecute = false,
                WorkingDirectory = projectRoot,
                CreateNoWindow = false,
            };
        }

        private static void LogPwshExit(string fileName, int exitCode)
        {
            if (exitCode != 0)
            {
                Debug.LogError($"[LiveOps] {fileName} exited with code {exitCode}.");
                return;
            }

            Debug.Log($"[LiveOps] {fileName} completed successfully.");
            AssetDatabase.Refresh();
        }

        private static string? FindPwsh()
        {
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv)) return null;
            string ext = Environment.OSVersion.Platform == PlatformID.Win32NT ? ".exe" : "";
            foreach (string? segment in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrEmpty(segment)) continue;
                string candidate = Path.Combine(segment, "pwsh" + ext);
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }
    }
}
