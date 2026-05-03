#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Scaffold.GraphFlow;
using Scaffold.GraphFlow.Editor;

namespace Scaffold.GraphFlow.M0.Editor
{
    /// <summary>
    /// CI/local: builds a standalone player that loads <see cref="PlayerSmoke.M0PlayerSmokeVerifier"/> scene and asserts PlayerPrefs.
    /// Requires: scene references a baked <c>.gfmsmoke</c> asset on the verifier field (author graph via Assets → Create → GraphFlow → M0 Smoke Graph).
    /// </summary>
    public static class GraphFlowM0PlayerSmokeBuild
    {
        const string ScenePath = "Assets/GraphFlowSandbox/Smoke/M0PlayerSmoke.unity";
        const string OutputDir = "Build/M0Smoke";

        [MenuItem("Tools/GraphFlow M0/Build Linux64 Player Smoke")]
        public static void BuildLinux64()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"Missing scene {ScenePath}. Create it or update the path in GraphFlowM0PlayerSmokeBuild.cs.");
                return;
            }

            Directory.CreateDirectory(OutputDir);
            var scenes = new[] { ScenePath };
            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutputDir, "M0Smoke.x86_64"),
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"Build failed: {report.summary.result}");
                return;
            }

            Debug.Log($"Built player at {opts.locationPathName}. Run with --batchmode -quit -projectPath ... or execute binary; check PlayerPrefs key GraphFlow.M0.LastResult == 42 for Mode1 smoke.");
        }
    }
}
#endif
