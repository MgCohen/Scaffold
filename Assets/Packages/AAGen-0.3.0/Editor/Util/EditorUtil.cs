using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AAGen
{
    public static class EditorUtil
    {
        /// <summary>
        /// Opens the folder in Explorer (Windows) or Finder (Mac).
        /// </summary>
        public static void LocatePersistentDataFolder()
        {
            try
            {
                var fullPath = Path.GetFullPath(Constants.FilePaths.PersistentDataFolder);
                FileUtils.EnsureDirectoryExist(fullPath);
                EditorUtility.RevealInFinder(fullPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"{e.Message}");
            }
        }

        public static void UnloadUnusedEditorMemory()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                EditorUtility.UnloadUnusedAssetsImmediate(true);
            }
            catch { /* best-effort */ }
        }
    }
}
