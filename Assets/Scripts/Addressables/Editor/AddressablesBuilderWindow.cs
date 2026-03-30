using UnityEngine;
using UnityEditor;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

[InitializeOnLoad]
public class AddressablesBuilderWindow : EditorWindow
{
    private enum BuildMode { Client, Server }
    private BuildMode currentMode = BuildMode.Client;
    private BuildTarget selectedSinglePlatform = BuildTarget.StandaloneWindows64;
    
    // --- EXPOSED SERVER SETTINGS ---
    private bool serverUploadToCloud = true;   
    private bool serverBuildPlayer = true;     
    private bool serverCleanStreaming = true;  

    [MenuItem("Tools/Addressables/Cloud Builder Pro")]
    public static void ShowWindow() => GetWindow<AddressablesBuilderWindow>("DO Sync");

    private void OnEnable()
    {
        selectedSinglePlatform = EditorUserBuildSettings.activeBuildTarget;
        if (selectedSinglePlatform == BuildTarget.StandaloneLinux64) currentMode = BuildMode.Server;
        else currentMode = BuildMode.Client;
        ValidatePlatformSelection();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("DIGITALOCEAN BUILDER", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // --- TABS ---
        bool isMobile = IsMobile(selectedSinglePlatform);
        EditorGUI.BeginDisabledGroup(isMobile);
        string[] modes = { "👤 CLIENT MODE", "🖥️ SERVER MODE" };
        int activeModeIndex = isMobile ? 0 : (int)currentMode;
        int newMode = GUILayout.Toolbar(activeModeIndex, modes, GUILayout.Height(30));
        if (!isMobile && newMode != (int)currentMode)
        {
            currentMode = (BuildMode)newMode;
            if(currentMode == BuildMode.Server) {
                serverUploadToCloud = true;
                serverBuildPlayer = true;
                serverCleanStreaming = true;
            }
            GUI.FocusControl(null); 
            ValidatePlatformSelection(); 
        }
        EditorGUI.EndDisabledGroup();
        if (isMobile && currentMode == BuildMode.Server) currentMode = BuildMode.Client;

        GUILayout.Space(15);
        DrawPlatformSelector();
        GUILayout.Space(10);
        DrawHorizontalLine(Color.gray);
        GUILayout.Space(10);

        if (currentMode == BuildMode.Client) DrawClientUI();
        else DrawServerUI();
    }

    private void DrawPlatformSelector()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Target Platform:", GUILayout.Width(100));
        List<BuildTarget> validTargets = GetValidPlatformsForMode(currentMode);
        string[] options = validTargets.Select(t => t.ToString()).ToArray();
        int currentIndex = validTargets.IndexOf(selectedSinglePlatform);
        if (currentIndex == -1) currentIndex = 0;
        int newIndex = EditorGUILayout.Popup(currentIndex, options);
        selectedSinglePlatform = validTargets[newIndex];
        if (IsMobile(selectedSinglePlatform)) currentMode = BuildMode.Client;
        GUILayout.EndHorizontal();
    }

    private void DrawClientUI()
    {
        string currentProfile = AddressablesBuildProcessor.PROFILE_CLIENT;
        EditorGUILayout.HelpBox($"[CLIENT BUILD]\nProfile: '{currentProfile}' -> Cloud.", MessageType.Info);
        GUILayout.Space(15);

        GUILayout.BeginHorizontal();
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button($"BUILD CLIENT & UPLOAD\n({selectedSinglePlatform})", GUILayout.Height(40)))
        {
             if (EditorUtility.DisplayDialog("Confirm Client Build", $"Build & Upload for {selectedSinglePlatform}?", "Yes", "Cancel"))
                RunStandardPipeline(selectedSinglePlatform, false);
        }
        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("UPDATE ONLY\n(Content)", GUILayout.Height(40)))
             RunStandardPipeline(selectedSinglePlatform, true);
        GUILayout.EndHorizontal();
    }

    private void DrawServerUI()
    {
        string currentProfile = AddressablesBuildProcessor.PROFILE_SERVER;
        
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Server Configuration", EditorStyles.boldLabel);
        
        serverUploadToCloud = EditorGUILayout.ToggleLeft(
            new GUIContent(" 1. Upload to Cloud (Hybrid)", "Also uploads bundles to DigitalOcean for future updates."), 
            serverUploadToCloud);
            
        serverBuildPlayer = EditorGUILayout.ToggleLeft(
            new GUIContent(" 2. Build Player (.exe/Headless)", "Compiles the actual Server executable."), 
            serverBuildPlayer);

        serverCleanStreaming = EditorGUILayout.ToggleLeft(
            new GUIContent(" 3. Clean StreamingAssets", "Delete embedded files after build? (Uncheck for debugging/Editor testing)."), 
            serverCleanStreaming);

        GUILayout.Space(5);
        
        // Status Logic
        string statusHeader = serverBuildPlayer ? (serverUploadToCloud ? "🚀 HYBRID SERVER BUILD" : "📦 OFFLINE/EMBED BUILD") : "🛠️ EDITOR/CONTENT ONLY";
        EditorGUILayout.HelpBox(statusHeader, MessageType.Info);

        GUILayout.EndVertical();

        // --- MAIN BUTTON ---
        GUILayout.Space(10);
        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f); // Light Green
        
        string actionName = serverBuildPlayer ? "BUILD FULL SERVER" : "BUILD CONTENT ONLY";
        
        if (GUILayout.Button($"{actionName}\n(Run All Steps)", GUILayout.Height(45)))
        {
            string confirmMsg = $"Start Server Build?\nPlatform: {selectedSinglePlatform}\nUpload: {serverUploadToCloud}\nPlayer: {serverBuildPlayer}";

            if (EditorUtility.DisplayDialog("Confirm Build", confirmMsg, "Yes", "Cancel"))
            {
                 // Runs the Orchestrator
                 _ = AddressablesBuildProcessor.RunServerBuild(
                     selectedSinglePlatform, 
                     serverUploadToCloud, 
                     serverBuildPlayer, 
                     serverCleanStreaming
                 );
            }
        }
        GUI.backgroundColor = Color.white;

        // --- NEW: INDIVIDUAL PROCESS BUTTONS ---
        GUILayout.Space(20);
        DrawHorizontalLine(Color.gray);
        GUILayout.Label("🛠️ Manual Server Steps (Isolated)", EditorStyles.boldLabel);
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("1. Build Addr.", GUILayout.Height(30))) 
            AddressablesBuildProcessor.BuildAddressablesContent(selectedSinglePlatform);
        
        if (GUILayout.Button("2. Copy to SA", GUILayout.Height(30))) 
            AddressablesBuildProcessor.CopyToStreamingAssets(selectedSinglePlatform);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("3. Build Exe", GUILayout.Height(30))) 
            AddressablesBuildProcessor.BuildPlayerExecutable(selectedSinglePlatform);

        if (GUILayout.Button("4. Clean SA", GUILayout.Height(30))) 
            AddressablesBuildProcessor.CleanStreamingAssets();
        GUILayout.EndHorizontal();
    }

    private void ValidatePlatformSelection()
    {
        List<BuildTarget> valid = GetValidPlatformsForMode(currentMode);
        if (!valid.Contains(selectedSinglePlatform)) selectedSinglePlatform = valid[0];
    }

    private List<BuildTarget> GetValidPlatformsForMode(BuildMode mode)
    {
        List<BuildTarget> targets = new List<BuildTarget>();
        if (mode == BuildMode.Client) { targets.Add(BuildTarget.StandaloneWindows64); targets.Add(BuildTarget.StandaloneOSX); targets.Add(BuildTarget.Android); targets.Add(BuildTarget.iOS); }
        else { targets.Add(BuildTarget.StandaloneLinux64); targets.Add(BuildTarget.StandaloneWindows64); targets.Add(BuildTarget.StandaloneOSX); }
        return targets;
    }

    private bool IsMobile(BuildTarget target) => target == BuildTarget.Android || target == BuildTarget.iOS;

    private async void RunStandardPipeline(BuildTarget target, bool isUpdate)
    {
        if (!AddressablesBuildProcessor.BuildClientAddressables(isUpdate, target)) return;
        string localPath = AddressablesBuildProcessor.GetLocalBuildPath(target, "Client");
        string remotePath = AddressablesBuildProcessor.GetRemotePath(target, "Client");
        await PerformSync(localPath, remotePath);
    }

    public static async Task PerformSync(string localRoot, string remoteFolder)
    {
        if (!Directory.Exists(localRoot)) { Debug.LogError($"Directory missing: {localRoot}"); return; }

        EditorUtility.DisplayProgressBar("Syncing", "Fetching remote list...", 0f);
        List<string> remoteFiles = await CloudSyncService.ListObjects(remoteFolder);
        string[] localFiles = Directory.GetFiles(localRoot, "*", SearchOption.TopDirectoryOnly);

        int uploadedCount = 0;
        int count = 0;
        foreach (string filePath in localFiles)
        {
            string fileName = Path.GetFileName(filePath);
            if (fileName.StartsWith(".")) continue;
            string remoteKey = $"{remoteFolder}/{fileName}";
            EditorUtility.DisplayProgressBar("Syncing", $"Processing {fileName}", (float)count / localFiles.Length);
            
            UploadResult result = await CloudSyncService.UploadFile(filePath, remoteKey, remoteFiles);
            if (result == UploadResult.Success) uploadedCount++;
            count++;
        }
        
        foreach (string remoteKey in remoteFiles)
        {
            string justName = remoteKey.Substring(remoteFolder.Length + 1); 
            if (!localFiles.Any(f => Path.GetFileName(f) == justName)) await CloudSyncService.DeleteObject(remoteKey);
        }
        
        EditorUtility.ClearProgressBar();
        Debug.Log($"[CloudBuilder] Sync Complete. Uploaded: {uploadedCount} files.");
    }

    private void DrawHorizontalLine(Color color, int thickness = 1, int padding = 10)
    {
        Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
        r.height = thickness;
        r.y += padding / 2;
        r.x -= 2;
        r.width += 6;
        EditorGUI.DrawRect(r, color);
    }
}