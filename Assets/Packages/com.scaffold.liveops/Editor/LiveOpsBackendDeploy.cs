using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using Debug = UnityEngine.Debug;
using UnityEditor;
using UnityEngine;

namespace Scaffold.LiveOps.Editor
{
    /// <summary>Sample: merge package <c>Backend~</c> into <c>LiveOps/</c> then <c>ugs deploy</c> the deploy solution (UGS CLI).</summary>
    internal static class LiveOpsBackendDeploy
    {
        private const string ugsCliAuthenticationHint =
            "The Unity Gaming Services CLI does not use the Editor’s sign-in. One-time: open a terminal (same user as this PC) and run: ugs login\n" +
            "and paste a Service Account Key ID and Secret from the Unity dashboard (UGS project → a team / service-accounts with deploy rights).\n" +
            "Or set UGS_CLI_SERVICE_KEY_ID and UGS_CLI_SERVICE_SECRET_KEY in your user or system environment, then deploy again. " +
            "Reference: services.docs.unity.com → UGS CLI → Login.";

        private const string ugsCliProjectRolesHint =
            "The service account you used for ugs login is signed in, but it does not have project roles for this UGS project (or the wrong project id). " +
            "The CLI must use the same Project ID as Edit → Project Settings → Services, and that service account needs roles assigned for that project in the Unity Dashboard (Service accounts).\n" +
            "For Remote Config deploy (.rc), Unity documents: assign at least Unity Environments Admin and Remote Config Admin for the Deploy command. " +
            "Details: services.docs.unity.com → UGS CLI → Troubleshooting → Project roles (Deploy command), and unauthorized-error-403.";

        internal static void Run()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            try
            {
                DoMergeAndRefreshStep(projectRoot);
                TryDeployStep(projectRoot);
            }
            catch (Exception ex)
            {
                LogDeployException(ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void DoMergeAndRefreshStep(string projectRoot)
        {
            EditorUtility.DisplayProgressBar("LiveOps Deploy", "Updating backend trees from packages...", 0.1f);
            LiveOpsBackendInstall.Run(projectRoot, false);
            AssetDatabase.Refresh();
        }

        private static void LogDeployException(Exception ex)
        {
            Debug.LogError("[LiveOps] Deploy: " + ex);
            EditorUtility.DisplayDialog("LiveOps Deploy", ex.Message, "OK");
        }

        private static void TryDeployStep(string projectRoot)
        {
            if (!UgsCliDeployContext.TryGetForDeploy(out string projectId, out string environmentName, out string err))
            {
                EditorUtility.DisplayDialog("LiveOps Deploy", err, "OK");
                return;
            }
            string sln = Path.Combine(projectRoot, "LiveOps", "LiveOps.Deploy.sln");
            if (!File.Exists(sln))
            {
                EditorUtility.DisplayDialog("LiveOps Deploy", "Solution not found: " + sln, "OK");
                return;
            }
            EditorUtility.DisplayProgressBar("LiveOps Deploy", "ugs deploy (Cloud Code)…", 0.5f);
            RunUgsWithFeedback(sln, projectId, environmentName);
        }

        private static void RunUgsWithFeedback(string sln, string projectId, string environmentName)
        {
            int code = RunUgsDeploySlnCore(sln, projectId, environmentName, out string log);
            string finalLog = AppendUgsFailureHintsIfNeeded(log, code);
            if (code != 0)
            {
                LogUgsError(code, finalLog);
                return;
            }
            Debug.Log("[LiveOps] ugs deploy completed.\n" + finalLog);
        }

        private static void LogUgsError(int code, string finalLog)
        {
            Debug.LogError("[LiveOps] ugs deploy failed (exit " + code + ").\n" + finalLog);
            EditorUtility.DisplayDialog("LiveOps Deploy", "ugs deploy failed (exit " + code + "). See Console for the full log.", "OK");
        }

        private static string AppendUgsFailureHintsIfNeeded(string log, int code)
        {
            if (code == 0)
            {
                return log;
            }
            if (UgsOutputIndicatesNotLoggedIn(log))
            {
                return log + Environment.NewLine + Environment.NewLine + ugsCliAuthenticationHint;
            }
            if (UgsOutputIndicatesForbiddenOrMissingProjectRoles(log))
            {
                return log + Environment.NewLine + Environment.NewLine + ugsCliProjectRolesHint;
            }
            return log;
        }

        private static int RunUgsDeploySlnCore(string slnPath, string projectId, string environmentName, out string log)
        {
            var logBuilder = new StringBuilder();
            using Process? p = TryStartUgsProcess(slnPath, projectId, environmentName);
            if (p is null)
            {
                log = "Could not start ugs. Install: npm i -g ugs — then open a new terminal and confirm `ugs --version`. The Editor must inherit PATH (restart Unity after installing if needed).";
                return -1;
            }
            return ReadUgsProcessResult(p, logBuilder, out log);
        }

        private static int ReadUgsProcessResult(Process p, StringBuilder logBuilder, out string log)
        {
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            logBuilder.AppendLine(FilterUgsCliNoiseCore(stdout));
            string errF = FilterUgsCliNoiseCore(stderr);
            if (!string.IsNullOrEmpty(errF))
            {
                logBuilder.AppendLine(errF);
            }
            log = logBuilder.ToString().Trim();
            return p.ExitCode;
        }

        private static Process? TryStartUgsProcess(string fullSlnPath, string projectId, string environmentName)
        {
            string pathQ = EscapeForCmdQuotedArg(fullSlnPath);
            string pidQ = EscapeForCmdQuotedArg(projectId);
            string envQ = EscapeForCmdQuotedArg(environmentName);
            try
            {
                if (Path.DirectorySeparatorChar == '\\')
                {
                    return StartUgsOnWindows(pathQ, pidQ, envQ);
                }
                return StartUgsOnMacOrLinux(pathQ, pidQ, envQ);
            }
            catch
            {
                return null;
            }
        }

        private static Process? StartUgsOnWindows(string pathQ, string pidQ, string envQ)
        {
            string a = "/d /c ugs deploy \"" + pathQ + "\" --project-id \"" + pidQ + "\" --environment-name \"" + envQ + "\"";
            return Process.Start(MakePsi(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe", a));
        }

        private static Process? StartUgsOnMacOrLinux(string pathQ, string pidQ, string envQ)
        {
            string a = "deploy \"" + pathQ + "\" --project-id \"" + pidQ + "\" --environment-name \"" + envQ + "\"";
            return Process.Start(MakePsi("ugs", a));
        }

        private static ProcessStartInfo MakePsi(string fileName, string arguments)
        {
            return new ProcessStartInfo { FileName = fileName, Arguments = arguments, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, };
        }

        private static string EscapeForCmdQuotedArg(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            return value.Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        private static bool UgsOutputIndicatesNotLoggedIn(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }
            return s.IndexOf("not logged into any service account", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("Please login using the", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool UgsOutputIndicatesForbiddenOrMissingProjectRoles(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }
            bool f = s.IndexOf("Status: 403", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("Code: 56", StringComparison.Ordinal) >= 0;
            bool n = s.IndexOf("No Permissions", StringComparison.Ordinal) >= 0 || s.IndexOf("Forbidden", StringComparison.Ordinal) >= 0;
            bool e = s.IndexOf("GetEnvironments", StringComparison.Ordinal) >= 0;
            return (f && n) || (n && e);
        }

        private static string FilterUgsCliNoiseCore(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }
            var sb = new StringBuilder();
            foreach (string line in s.Split(new[] { '\r', '\n' }, StringSplitOptions.None))
            {
                if (IsUgsNoiseLine(line))
                {
                    continue;
                }
                sb.AppendLine(line);
            }
            return sb.ToString().Trim();
        }

        private static bool IsUgsNoiseLine(string line)
        {
            if (line.IndexOf("DeprecationWarning", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            if (line.IndexOf("[DEP0", StringComparison.Ordinal) >= 0)
            {
                return true;
            }
            if (line.IndexOf("trace-deprecation", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            return line.IndexOf("(Use `node", StringComparison.Ordinal) >= 0;
        }
    }
}
