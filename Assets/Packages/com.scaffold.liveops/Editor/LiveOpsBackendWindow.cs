using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Scaffold.LiveOps.Editor
{
    /// <summary>Sample: editor window for LiveOps <c>Backend~/</c> sync and UGS CLI deploy.</summary>
    public sealed class LiveOpsBackendWindow : EditorWindow
    {
        private const string kComScaffoldPrefix = "com.scaffold.";
        private const string kBackendTilde = "Backend~";
        private const float kMinNameWidth = 180f;
        private const float kDeployBadgeW = 44f;
        private const float kScaffoldBadgeW = 52f;
        private const float kActionButtonW = 64f;
        private readonly List<PackageRow> rows = new();
        private Vector2 scroll;
        private bool needsRescan = true;

        private void OnEnable()
        {
            needsRescan = true;
        }

        private void OnGUI()
        {
            if (needsRescan)
            {
                RescanList();
                needsRescan = false;
            }
            DrawToolbar();
            DrawHelp();
            DrawScrolledList();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawToolbarRescan();
                DrawToolbarUpdateAll();
                DrawToolbarDevAndDeploy();
            }
        }

        private void DrawToolbarRescan()
        {
            if (GUILayout.Button("Rescan", EditorStyles.toolbarButton))
            {
                needsRescan = true;
            }
        }

        private void DrawToolbarUpdateAll()
        {
            if (GUILayout.Button("Update All", EditorStyles.toolbarButton))
            {
                UpdateAll();
            }
        }

        private void DrawToolbarDevAndDeploy()
        {
#if SCAFFOLD_LIVEOPS_PACKAGE_DEV
            if (GUILayout.Button("Refresh All", EditorStyles.toolbarButton))
            {
                RefreshAll();
            }
#endif
            if (GUILayout.Button("Deploy", EditorStyles.toolbarButton))
            {
                LiveOpsBackendDeploy.Run();
            }
        }

        private void DrawHelp()
        {
            const string helpText = "Update merges each package’s Backend~ into LiveOps/. Refresh (dev) mirrors LiveOps/ back into package Backend~. Deploy runs Update All, then ugs deploy on LiveOps/LiveOps.Deploy.sln (requires ugs CLI + service account).";
            EditorGUILayout.HelpBox(helpText, MessageType.Info);
        }

        private void DrawScrolledList()
        {
            using (var scope = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = scope.scrollPosition;
                foreach (PackageRow row in rows)
                {
                    DrawOneRow(row);
                }
            }
        }

        private void DrawOneRow(PackageRow row)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawRowLabels(row);
                DrawRowActions(row);
            }
        }

        private void DrawRowLabels(PackageRow row)
        {
            EditorGUILayout.LabelField(row.Name, GUILayout.MinWidth(kMinNameWidth));
            if (row.HasDeploy)
            {
                GUILayout.Label("Deploy", EditorStyles.miniLabel, GUILayout.Width(kDeployBadgeW));
            }
            if (row.HasScaffold)
            {
                GUILayout.Label("Scaffold", EditorStyles.miniLabel, GUILayout.Width(kScaffoldBadgeW));
            }
        }

        private void DrawRowActions(PackageRow row)
        {
            if (GUILayout.Button("Update", GUILayout.Width(kActionButtonW)))
            {
                UpdateOne(row);
            }
#if SCAFFOLD_LIVEOPS_PACKAGE_DEV
            if (GUILayout.Button("Refresh", GUILayout.Width(kActionButtonW)))
            {
                RefreshOne(row);
            }
#endif
        }

        private void UpdateAll()
        {
            string root = BuildProjectRoot();
            try
            {
                LiveOpsBackendInstall.Run(root, false);
            }
            catch (Exception ex)
            {
                Debug.LogError("[LiveOps] " + ex.Message);
                return;
            }
            AssetDatabase.Refresh();
            Debug.Log("[LiveOps] Update All completed.");
        }

        private void UpdateOne(PackageRow row)
        {
            if (!RunUpdateForRowWithCatch(row))
            {
                return;
            }
            AssetDatabase.Refresh();
        }

        private bool RunUpdateForRowWithCatch(PackageRow row)
        {
            try
            {
                return RunUpdateForRow(row);
            }
            catch (Exception ex)
            {
                Debug.LogError("[LiveOps] " + ex.Message);
                return false;
            }
        }

        private bool RunUpdateForRow(PackageRow row)
        {
            string root = BuildProjectRoot();
            LiveOpsBackendInstallContext ctx = LiveOpsBackendInstall.CreateContext(root);
            if (!LiveOpsBackendInstall.MergeOnePackage(ctx, row.Path, false))
            {
                return false;
            }
            LiveOpsBackendInstall.EnsureGameDir(ctx.DestLive, false);
            if (row.IsHost)
            {
                LiveOpsBackendInstall.ApplyHostTemplateFiles(ctx, false);
            }
            return true;
        }

#if SCAFFOLD_LIVEOPS_PACKAGE_DEV
        private void RefreshAll()
        {
            try
            {
                LiveOpsBackendRefresh.RunAll(BuildProjectRoot(), false);
            }
            catch (Exception ex)
            {
                Debug.LogError("[LiveOps] " + ex.Message);
                return;
            }
            AssetDatabase.Refresh();
            Debug.Log("[LiveOps] Refresh All completed.");
        }

        private void RefreshOne(PackageRow row)
        {
            try
            {
                LiveOpsBackendInstallContext c = LiveOpsBackendInstall.CreateContext(BuildProjectRoot());
                LiveOpsBackendRefresh.RefreshOnePackage(c, row.Path, false);
            }
            catch (Exception ex)
            {
                Debug.LogError("[LiveOps] " + ex.Message);
                return;
            }
            AssetDatabase.Refresh();
        }
#endif

        private void RescanList()
        {
            rows.Clear();
            FillRowsFromDisk();
        }

        private void FillRowsFromDisk()
        {
            string packagesRoot = Path.Combine(BuildProjectRoot(), "Assets", "Packages");
            if (!Directory.Exists(packagesRoot))
            {
                return;
            }
            foreach (string packageDir in Directory.EnumerateDirectories(packagesRoot))
            {
                TryAppendPackageRow(packageDir);
            }
            SortPackageRowsInPlace();
        }

        private void TryAppendPackageRow(string packageDir)
        {
            string n = Path.GetFileName(packageDir);
            if (!n.StartsWith(kComScaffoldPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            string b = Path.Combine(packageDir, kBackendTilde);
            if (!Directory.Exists(b))
            {
                return;
            }
            string full = Path.GetFullPath(packageDir);
            bool d = Directory.Exists(Path.Combine(b, "Deploy"));
            bool s = Directory.Exists(Path.Combine(b, "Scaffold"));
            bool h = LiveOpsBackendRefresh.IsHostPackage(packageDir);
            rows.Add(new PackageRow(n, full, d, s, h));
        }

        private void SortPackageRowsInPlace()
        {
            rows.Sort(ComparePackageRowsByName);
        }

        private int ComparePackageRowsByName(PackageRow a, PackageRow b)
        {
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        }

        private string BuildProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private readonly struct PackageRow
        {
            internal PackageRow(string name, string path, bool hasDeploy, bool hasScaffold, bool isHost)
            {
                Name = name;
                Path = path;
                HasDeploy = hasDeploy;
                HasScaffold = hasScaffold;
                IsHost = isHost;
            }
            internal string Name { get; }
            internal string Path { get; }
            internal bool HasDeploy { get; }
            internal bool HasScaffold { get; }
            internal bool IsHost { get; }
        }
    }
}
