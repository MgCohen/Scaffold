using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class AddressablesCleanupTool : EditorWindow
{
    // --- UI STATE ---
    private bool cleanAllPlatforms = false;
    private int selectedPlatformIndex = 0;
    private string[] availablePlatforms = new string[0];
    private List<string> filesToDeleteUI = new List<string>(); // UI specific list

    [MenuItem("Tools/Addressables/Cleanup ServerData")]
    public static void ShowWindow()
    {
        GetWindow<AddressablesCleanupTool>("Addressables Cleanup");
    }

    private void OnEnable()
    {
        RefreshPlatformList();
    } 
    private void OnFocus()
    {
        RefreshPlatformList();
    }

    // --- PUBLIC API FOR BUILDER ---
    
    /// <summary>
    /// Runs a silent cleanup for a specific platform. Called by AddressablesBuilder.
    /// </summary>
    public static void RunCleanup(string platformName)
    {
        Debug.Log($"[Cleanup] Starting auto-cleanup for: {platformName}");
        List<string> obsoleteFiles = ScanPlatformForObsoleteFiles(platformName);
        
        if (obsoleteFiles.Count > 0)
        {
            DeleteFileList(obsoleteFiles);
        }
        else
        {
            Debug.Log($"[Cleanup] {platformName} is already clean.");
        }
    }

    // --- CORE LOGIC ---

    private static List<string> ScanPlatformForObsoleteFiles(string platformName)
    {
        List<string> obsoleteFiles = new List<string>();
        
        string projectPath = Directory.GetCurrentDirectory();
        string platformPath = Path.Combine(projectPath, "ServerData", platformName);

        if (!Directory.Exists(platformPath))
        {
            Debug.LogWarning($"[Cleanup] Skipped {platformName}: Folder not found at {platformPath}");
            return obsoleteFiles;
        }

        // Find Catalog
        FileInfo[] catalogFiles = new DirectoryInfo(platformPath).GetFiles("catalog*.json")
            .OrderByDescending(f => f.LastWriteTime)
            .ToArray();

        if (catalogFiles.Length == 0)
        {
            Debug.LogWarning($"[Cleanup] Skipped {platformName}: No catalog_*.json found.");
            return obsoleteFiles;
        }

        FileInfo activeCatalog = catalogFiles[0];
        string catalogContent = File.ReadAllText(activeCatalog.FullName);
        string[] allFiles = Directory.GetFiles(platformPath, "*", SearchOption.TopDirectoryOnly);

        foreach (string filePath in allFiles)
        {
            string fileName = Path.GetFileName(filePath);
            if (fileName.StartsWith(".")) continue;

            // Whitelist Logic: Keep Catalog, Hash, and referenced bundles
            if (fileName == activeCatalog.Name || 
                fileName == activeCatalog.Name.Replace(".json", ".hash") || 
                catalogContent.Contains(fileName))
            {
                continue; // Keep
            }

            obsoleteFiles.Add(filePath);
        }

        return obsoleteFiles;
    }

    private static void DeleteFileList(List<string> files)
    {
        int count = 0;
        foreach (string filePath in files)
        {
            if (!File.Exists(filePath))
            {
                continue;
            }

            File.Delete(filePath);
            count++;
        }
        Debug.Log($"[Cleanup] Deleted {count} obsolete files.");
        AssetDatabase.Refresh();
    }

    // --- EDITOR WINDOW UI LOGIC ---

    private void RefreshPlatformList()
    {
        string serverDataRoot = Path.Combine(Directory.GetCurrentDirectory(), "ServerData");
        if (Directory.Exists(serverDataRoot))
        {
            availablePlatforms = Directory.GetDirectories(serverDataRoot)
                .Select(Path.GetFileName).ToArray();
        }
        else availablePlatforms = new string[0];
    }

    private void OnGUI()
    {
        GUILayout.Label("Cleanup 'ServerData' Folder", EditorStyles.boldLabel);
        GUILayout.Space(5);

        if (availablePlatforms.Length == 0)
        {
            EditorGUILayout.HelpBox("No 'ServerData' folder found.", MessageType.Warning);
            if (GUILayout.Button("Refresh"))
            {
                RefreshPlatformList();
            }
            return;
        }

        cleanAllPlatforms = EditorGUILayout.ToggleLeft("Clean All Platforms", cleanAllPlatforms);

        if (!cleanAllPlatforms)
        {
            if (selectedPlatformIndex >= availablePlatforms.Length) selectedPlatformIndex = 0;
            selectedPlatformIndex = EditorGUILayout.Popup("Platform", selectedPlatformIndex, availablePlatforms);
        }

        if (GUILayout.Button("1. Scan for Obsolete Bundles", GUILayout.Height(30)))
        {
            filesToDeleteUI.Clear();
            if (cleanAllPlatforms)
            {
                foreach (string p in availablePlatforms) filesToDeleteUI.AddRange(ScanPlatformForObsoleteFiles(p));
            }
            else
            {
                filesToDeleteUI.AddRange(ScanPlatformForObsoleteFiles(availablePlatforms[selectedPlatformIndex]));
            }
        }

        if (filesToDeleteUI.Count > 0)
        {
            EditorGUILayout.HelpBox($"Found {filesToDeleteUI.Count} obsolete files.", MessageType.Warning);
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("2. DELETE Obsolete Files", GUILayout.Height(30)))
            {
                DeleteFileList(filesToDeleteUI);
                filesToDeleteUI.Clear();
            }
            GUI.backgroundColor = Color.white;

            using (EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope(Vector2.zero))
            {
                foreach (string file in filesToDeleteUI)
                    GUILayout.Label(Path.GetFileName(file), EditorStyles.miniLabel);
            }
        }
    }
}