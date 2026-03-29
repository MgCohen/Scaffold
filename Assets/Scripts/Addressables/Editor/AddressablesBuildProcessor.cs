using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

public static class AddressablesBuildProcessor
{
    private const string SERVER_DATA = "ServerData"; 
    private const string BIN_FILE = "addressables_content_state.bin";
    private const string ENVIRONMENT_NAME_VAR = "EnvironmentName"; 
    private const string DEFAULT_ENV = "production"; 

    public const string PROFILE_SERVER = "Server"; 
    public const string PROFILE_CLIENT = "Default"; // Restored Client Profile
    public const string APP_NAME = "Scaffold";

    // ===================================================================================
    //  CLIENT BUILD LOGIC (Restored)
    // ===================================================================================
    public static bool BuildClientAddressables(bool isUpdate, BuildTarget target)
    {
        if (!SetProfile(PROFILE_CLIENT)) return false;
        try
        {
            Stopwatch timer = Stopwatch.StartNew();
            if (!isUpdate) 
            { 
                Debug.Log($"[Client] Building Clean Content for {target}...");
                AddressableAssetSettings.CleanPlayerContent(); 
                AddressableAssetSettings.BuildPlayerContent(); 
            }
            else
            {
                Debug.Log($"[Client] Updating Content for {target}...");
                string binPath = Path.Combine(GetLocalBuildPath(target, "Client"), BIN_FILE);
                if (!File.Exists(binPath)) 
                { 
                    Debug.LogError($"Missing .bin file at {binPath}. Cannot do Update."); 
                    return false; 
                }
                ContentUpdateScript.BuildContentUpdate(AddressableAssetSettingsDefaultObject.Settings, binPath);
            }
            timer.Stop();
            Debug.Log($"[Client] ✅ Build Complete ({timer.Elapsed.TotalSeconds:F1}s)");
            return true;
        }
        catch (Exception e) 
        { 
            Debug.LogError($"[Client] Build Exception: {e.Message}"); 
            return false; 
        }
    }

    // ===================================================================================
    //  SERVER MODULAR STEPS
    // ===================================================================================

    // --- 1. BUILD ADDRESSABLES ---
    public static void BuildAddressablesContent(BuildTarget target)
    {
        if (!SetProfile(PROFILE_SERVER))
        {
            return;
        }
            
        // FORCE DIRECTORY EXISTENCE
        string localPath = GetLocalBuildPath(target, "Server");
        if (!Directory.Exists(localPath))
        {
            Directory.CreateDirectory(localPath);
            Debug.Log($"[Step 1] Created missing directory: {localPath}");
        }

        Stopwatch timer = Stopwatch.StartNew();
        Debug.Log($"[Step 1] Building Addressables for {target}...");

        AddressableAssetSettings.CleanPlayerContent();
        AddressableAssetSettings.BuildPlayerContent();
        
        timer.Stop();
        Debug.Log($"[Step 1] ✅ Addressables Built ({timer.Elapsed.TotalSeconds:F1}s)");
    }

    // --- 2. UPLOAD (Async) ---
    public static async Task UploadServerContent(BuildTarget target)
    {
        string localPath = GetLocalBuildPath(target, "Server");
        string remotePath = GetRemotePath(target, "Server");
        
        // Ensure the upload logic calls your window's Sync method
        Debug.Log($"[Step 2] Syncing to Cloud...");
        await AddressablesBuilderWindow.PerformSync(localPath, remotePath);
        Debug.Log($"[Step 2] ✅ Upload Complete.");
    }

    // --- 3. COPY TO STREAMING ASSETS ---
    public static void CopyToStreamingAssets(BuildTarget target)
    {
        string localPath = GetLocalBuildPath(target, "Server");
        string platformStr = GetPlatformString(target);
        
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        string envName = settings.profileSettings.GetValueByName(settings.activeProfileId, ENVIRONMENT_NAME_VAR);
        if (string.IsNullOrEmpty(envName)) envName = DEFAULT_ENV;

        string destDir = Path.Combine(Application.streamingAssetsPath, "aa", "Server", envName, platformStr);

        Debug.Log($"[Step 3] Copying files to: {destDir}");
        CopyDirectory(localPath, destDir);
        GenerateShippedManifest(destDir);
        
        // CRITICAL FIX: Refresh database so Unity sees the new files before building player
        AssetDatabase.Refresh(); 
        Debug.Log("[Step 3] ✅ Copy Complete & Database Refreshed.");
    }

    // --- 4. BUILD PLAYER ---
    public static void BuildPlayerExecutable(BuildTarget target)
    {
        string platformStr = GetPlatformString(target);
        Debug.Log($"[Step 4] Building Player Executable for {platformStr}...");

        string buildOutputFolder = Path.Combine("Builds", "Server", platformStr);
        string buildName = APP_NAME;

        if (target == BuildTarget.StandaloneWindows64) buildName += ".exe";
        else if (target == BuildTarget.StandaloneLinux64) buildName += ".x86_64";
        else if (target == BuildTarget.StandaloneOSX) buildName += ".app";

        string fullPath = Path.Combine(buildOutputFolder, buildName);
        Directory.CreateDirectory(buildOutputFolder);

        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        options.locationPathName = fullPath;
        options.target = target;
        options.options = BuildOptions.EnableHeadlessMode;

        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[Step 4] ✅ Build Success: {fullPath}");
            EditorUtility.RevealInFinder(fullPath);
        }
        else
        {
            Debug.LogError($"[Step 4] ❌ Build Failed. Check Console for details.");
        }
    }

    // --- 5. CLEANUP ---
    public static void CleanStreamingAssets()
    {
        string aaFolder = Path.Combine(Application.streamingAssetsPath, "aa");
        string manifest = Path.Combine(Application.streamingAssetsPath, "shipped_bundles.txt");

        if (Directory.Exists(aaFolder)) Directory.Delete(aaFolder, true);
        if (File.Exists(aaFolder + ".meta")) File.Delete(aaFolder + ".meta");
        if (File.Exists(manifest)) File.Delete(manifest);
        if (File.Exists(manifest + ".meta")) File.Delete(manifest + ".meta");

        AssetDatabase.Refresh();
        Debug.Log("[Step 5] 🧹 StreamingAssets Cleaned.");
    }

    // ===================================================================================
    //  FULL ORCHESTRATOR (Restored)
    // ===================================================================================
    public static async Task RunServerBuild(BuildTarget target, bool shouldUpload, bool buildPlayer, bool cleanStreamingAssets)
    {
        Stopwatch totalTimer = Stopwatch.StartNew();
        
        // 1. Build Content
        BuildAddressablesContent(target);

        // 2. Upload (Conditional)
        if (shouldUpload) await UploadServerContent(target);

        // 3. Copy
        CopyToStreamingAssets(target);

        // 4. Build Player (Synchronous call to avoid crash)
        if (buildPlayer) BuildPlayerExecutable(target);

        // 5. Cleanup
        if (cleanStreamingAssets) CleanStreamingAssets();
        else Debug.LogWarning($"[Build] ⚠️ StreamingAssets cleanup skipped.");

        totalTimer.Stop();
        EditorUtility.DisplayDialog("Build Complete", $"✅ Server Build Chain Finished in {totalTimer.Elapsed.TotalSeconds:F1}s", "OK");
    }

    // ===================================================================================
    //  HELPERS
    // ===================================================================================
    private static void GenerateShippedManifest(string destDir)
    {
        if (!Directory.Exists(destDir)) return;
        var files = Directory.GetFiles(destDir, "*.bundle", SearchOption.AllDirectories);
        List<string> fileNames = files.Select(Path.GetFileName).ToList();
        File.WriteAllLines(Path.Combine(Application.streamingAssetsPath, "shipped_bundles.txt"), fileNames);
    }

    public static string GetLocalBuildPath(BuildTarget target, string subFolder)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        string envName = settings.profileSettings.GetValueByName(settings.activeProfileId, ENVIRONMENT_NAME_VAR);
        if (string.IsNullOrEmpty(envName)) envName = DEFAULT_ENV;
        return Path.Combine(Directory.GetCurrentDirectory(), SERVER_DATA, subFolder, envName, GetPlatformString(target));
    }

    public static string GetRemotePath(BuildTarget target, string subFolder)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        string envName = settings.profileSettings.GetValueByName(settings.activeProfileId, ENVIRONMENT_NAME_VAR);
        if (string.IsNullOrEmpty(envName)) envName = DEFAULT_ENV;
        return $"Addressables/{subFolder}/{envName}/{GetPlatformString(target)}";
    }

    private static bool SetProfile(string profileName)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        string profileId = settings.profileSettings.GetProfileId(profileName);
        if (string.IsNullOrEmpty(profileId)) { Debug.LogError($"Profile '{profileName}' not found!"); return false; }
        if (settings.activeProfileId != profileId) { settings.activeProfileId = profileId; EditorUtility.SetDirty(settings); }
        return true;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        if(!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string dest = Path.Combine(destDir, file.Substring(sourceDir.Length + 1));
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(file, dest, true);
        }
    }

    private static string GetPlatformString(BuildTarget target)
    {
        switch (target) {
            case BuildTarget.StandaloneLinux64: return "StandaloneLinux64";
            case BuildTarget.StandaloneOSX: return "StandaloneOSX";
            case BuildTarget.StandaloneWindows64: return "StandaloneWindows64";
            case BuildTarget.Android: return "Android";
            case BuildTarget.iOS: return "iOS";
            default: return target.ToString();
        }
    }
}