using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AAGen
{
    public class AddressableReferenceTargetsWindow : EditorWindow
    {
        [Serializable]
        private class ScanOptions
        {
            public DefaultAsset inputFolder;
            public bool includeSubfolders = true; // kept for UI parity
            public bool includeScenes = true;
            public string outputJsonPath = "Assets/AddressableReferenceTargets.json";
        }

        private ScanOptions _options = new ScanOptions();

        // ToDo: Must depend on project size; higher = fewer pauses, lower = more frequent memory relief
        private const int kMemReliefStride = 800;
        
        [MenuItem(Constants.Menus.Root + "Addressable Reference Scanner", priority = Constants.Menus.AAGenMenuPriority)]
        private static void ShowWindow()
        {
            GetWindow<AddressableReferenceTargetsWindow>("Addressable Ref Scanner");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Export all Addressable target assets that are referenced via AssetReference",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope("box"))
            {
                _options.inputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                    "Input Folder", _options.inputFolder, typeof(DefaultAsset), false);

                _options.includeSubfolders =
                    EditorGUILayout.ToggleLeft("Include Subfolders", _options.includeSubfolders);

                _options.includeScenes =
                    EditorGUILayout.ToggleLeft("Include Scenes (slower)", _options.includeScenes);

                _options.outputJsonPath =
                    EditorGUILayout.TextField("Output JSON Path", _options.outputJsonPath);
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_options.inputFolder == null))
            {
                if (GUILayout.Button("Generate JSON"))
                    GenerateJson();
            }
        }

        private void GenerateJson()
        {
            if (_options.inputFolder == null)
            {
                ShowNotification(new GUIContent("Please assign an input folder."));
                return;
            }

            var folderPath = AssetDatabase.GetAssetPath(_options.inputFolder);
            if (string.IsNullOrEmpty(folderPath))
            {
                ShowNotification(new GUIContent("Invalid input folder."));
                return;
            }

            // Build a compact, typed candidate list (prefabs + SOs + optional scenes)
            // This is lightweight (just GUID strings → paths), not loaded UnityEngine.Objects.
            var candidates = BuildTypedCandidateList(folderPath, _options.includeScenes);
            var total = candidates.Count;

            var referencedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var processed = 0;

            try
            {
                for (int i = 0; i < total; i++)
                {
                    var path = candidates[i];

                    // Cancellable progress bar
                    float progress = (float)i / Math.Max(1, total);
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Scanning for Addressable targets",
                            $"{i + 1}/{total}  {path}",
                            progress))
                    {
                        Debug.LogWarning("Addressable Reference scan cancelled by user.");
                        break;
                    }

                    try
                    {
                        if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                        {
                            if (_options.includeScenes)
                                ExtractFromScene(path, referencedGuids);
                        }
                        else if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                        {
                            ExtractFromPrefab(path, referencedGuids);
                        }
                        else
                        {
                            ExtractFromNonPrefabAsset(path, referencedGuids);
                        }

                        processed++;
                    }
                    catch (Exception ex)
                    {
                        // Robustness: skip corrupted/incompatible assets and continue
                        Debug.LogWarning($"[AddrRef Scan] Skipped '{path}' due to error: {ex.Message}");
                    }

                    // Periodic memory relief to keep the Editor heap & native mem stable
                    if (processed % kMemReliefStride == 0)
                        EditorUtil.UnloadUnusedEditorMemory();
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                // One final sweep
                try
                {
                    System.GC.Collect();
                    System.GC.WaitForPendingFinalizers();
                    EditorUtility.UnloadUnusedAssetsImmediate(true);
                }
                catch { /* ignore */ }
            }

            // Convert GUIDs → unique asset paths, filter out empties, sort for determinism
            var referencedPaths = referencedGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Save JSON (simple array of paths)
            var json = JsonHelper.ToJsonArray(referencedPaths);
            var outPath = _options.outputJsonPath;

            try
            {
                var outDir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                    Directory.CreateDirectory(outDir);

                File.WriteAllText(outPath, json);
                AssetDatabase.Refresh();

                Debug.Log($"[AddrRef Scan] Processed {processed}/{total} items. " +
                          $"Wrote {referencedPaths.Count} target paths to: {outPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddrRef Scan] Failed writing JSON to '{outPath}': {ex.Message}");
            }
        }

        // ---------- Discovery (typed and compact) ----------

        private static List<string> BuildTypedCandidateList(string folderPath, bool includeScenes)
        {
            var searchIn = new[] { folderPath };
            var paths = new List<string>(capacity: 4096); // start reasonable; list grows as needed

            // Prefabs
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", searchIn))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (!string.IsNullOrEmpty(p))
                    paths.Add(p);
            }

            // ScriptableObjects
            foreach (var g in AssetDatabase.FindAssets("t:ScriptableObject", searchIn))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (!string.IsNullOrEmpty(p))
                    paths.Add(p);
            }

            // Scenes (optional)
            if (includeScenes)
            {
                foreach (var g in AssetDatabase.FindAssets("t:Scene", searchIn))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (!string.IsNullOrEmpty(p))
                        paths.Add(p);
                }
            }

            // Deduplicate; many projects have GUID collisions across filters
            return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        // ---------- Extraction (streamed & promptly unloaded) ----------

        private static void ExtractFromPrefab(string prefabPath, HashSet<string> referencedGuids)
        {
            GameObject go = null;
            try
            {
                go = PrefabUtility.LoadPrefabContents(prefabPath);
            }
            catch
            {
                // Bad/corrupt prefab—skip
                return;
            }

            try
            {
                foreach (var comp in go.GetComponentsInChildren<Component>(true))
                    ExtractFromUnityObject(comp, referencedGuids);
            }
            finally
            {
                try { PrefabUtility.UnloadPrefabContents(go); } catch { /* ignore */ }
            }
        }

        private static void ExtractFromNonPrefabAsset(string assetPath, HashSet<string> referencedGuids)
        {
            // Main asset
            UnityEngine.Object main = null;
            try
            {
                main = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (main != null)
                    ExtractFromUnityObject(main, referencedGuids);
            }
            catch
            {
                // skip this file
            }
            finally
            {
                if (main != null)
                {
                    try { Resources.UnloadAsset(main); } catch { /* ignore */ }
                }
            }

            // Sub-asset representations (e.g., Sprites in a Texture, sub-SOs, etc.)
            UnityEngine.Object[] reps = null;
            try
            {
                reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            }
            catch
            {
                // ignore
            }

            if (reps != null)
            {
                foreach (var rep in reps)
                {
                    try
                    {
                        if (rep != null)
                            ExtractFromUnityObject(rep, referencedGuids);
                    }
                    catch { /* ignore */ }
                    finally
                    {
                        if (rep != null)
                        {
                            try { Resources.UnloadAsset(rep); } catch { /* ignore */ }
                        }
                    }
                }
            }
        }

        private static void ExtractFromScene(string scenePath, HashSet<string> referencedGuids)
        {
            var scene = default(UnityEngine.SceneManagement.Scene);
            try
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }
            catch
            {
                return; // cannot open scene—skip
            }

            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    // Include GameObject (for any custom serialized data on it)
                    ExtractFromUnityObject(root, referencedGuids);

                    // Components
                    var comps = root.GetComponentsInChildren<Component>(true);
                    foreach (var c in comps)
                        ExtractFromUnityObject(c, referencedGuids);
                }
            }
            catch
            {
                // scene content problem—skip and continue
            }
            finally
            {
                try { EditorSceneManager.CloseScene(scene, true); } catch { /* ignore */ }
            }
        }

        private static void ExtractFromUnityObject(UnityEngine.Object obj, HashSet<string> referencedGuids)
        {
            if (obj == null) return;

            SerializedObject so = null;
            try
            {
                so = new SerializedObject(obj);
            }
            catch
            {
                return; // object cannot be serialized—skip
            }

            try
            {
                var iterator = so.GetIterator();
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = true;

                    // We simply look for the serialized backing string 'm_AssetGUID',
                    // which is how Addressables store AssetReference targets.
                    if (iterator.propertyType == SerializedPropertyType.String &&
                        iterator.name == "m_AssetGUID")
                    {
                        var guid = iterator.stringValue;
                        if (!string.IsNullOrEmpty(guid))
                            referencedGuids.Add(guid);
                    }
                }
            }
            catch
            {
                // corrupted/incompatible serialized data—ignore
            }
        }

        // ---------- Minimal JSON helper (array of strings) ----------

        private static class JsonHelper
        {
            public static string ToJsonArray(IEnumerable<string> items)
            {
                var escaped = items.Select(Escape);
                return "[\n" + string.Join(",\n", escaped) + "\n]";
            }

            private static string Escape(string s)
            {
                return "\"" + (s ?? string.Empty)
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"") + "\"";
            }
        }
    }
}
