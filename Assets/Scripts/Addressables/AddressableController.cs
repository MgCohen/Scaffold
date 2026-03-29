using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scaffold.Logging;
using Scaffold.Shared;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using Object = UnityEngine.Object;

namespace Scaffold.Addressable
{
    public class AddressableController : MonoBehaviour
    {
        public AssetReferenceScene sceneAssetReference;
        
        public static bool IsInitialized { get; private set; }

        [Tooltip("Leave empty to download ALL assets.")]
        [SerializeField] 
        private string labelToDownload;

        private static readonly Dictionary<string, AsyncOperationHandle> activeHandles = new Dictionary<string, AsyncOperationHandle>();
        
        public event Action<long, long> OnDownloadProgress; 
        public event Action OnDownloadComplete;
        
        private void Awake()
        {
            // Only run this redirection logic on the Server (Headless or Linux)
            // This ensures Clients (Players) still download normally from the cloud.
            if (IsServerBuild())
            {
                SetupServerFallback();
            }
        }

        private bool IsServerBuild()
        {
#if SERVER
            return true;
#endif
            return Application.isBatchMode || Application.platform == RuntimePlatform.LinuxPlayer;
        }

        private void SetupServerFallback()
        {
            GameDebug.Log("[AddressableController] Initializing Server Hybrid Fallback...");
    
            Addressables.InternalIdTransformFunc = (IResourceLocation location) =>
            {
                // 1. Intercept Remote URLs (HTTP/HTTPS)
                // We strictly check for ".bundle" to ensure we don't accidentally intercept 
                // the Catalog (.json) or Hash (.hash) files, which MUST come from the cloud 
                // to detect if updates are available.
                if (location.InternalId.StartsWith("http") && location.InternalId.EndsWith(".bundle"))
                {
                    string fileName = Path.GetFileName(location.InternalId);
            
                    // 2. Check for the file in StreamingAssets (where we embedded it)
                    // Path: StreamingAssets/aa/Server/[Env]/[Platform]/filename
                    string localDirectory = Path.Combine(Application.streamingAssetsPath, "aa");
            
                    // Search for the file. This handles folder structure variations automatically.
                    string candidatePath = FindFileInFolder(localDirectory, fileName);

                    if (!string.IsNullOrEmpty(candidatePath))
                    {
                        GameDebug.Log($"[Server-Hybrid] Found embedded bundle: {fileName}. Loading from Disk.");
                
                        // Return the LOCAL path. Addressables will load this file from disk 
                        // instead of using the URL.
                        return candidatePath; 
                    }
                }
        
                // 3. Default: Use the original URL (download from DigitalOcean)
                return location.InternalId;
            };
        }

        private string FindFileInFolder(string rootPath, string fileName)
        {
            if (!Directory.Exists(rootPath))
            {
                return null;
            }
            // Simple recursive search - fast enough for startup
            string[] files = Directory.GetFiles(rootPath, fileName, SearchOption.AllDirectories);
            return files.Length > 0 ? files[0] : null;
        }

        public void Start()
        {
            StartCoroutine(Initialize());
        }
        
        public void Update()
        {
            #if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.C)) 
            {
                GameDebug.Log("[Cache] Clearing ALL Addressables cache...");
                ClearCacheAndRestart();
            }
            #endif
        }

        public IEnumerator Initialize()
        {
            DontDestroyOnLoad(gameObject);
            if (IsInitialized)
            {
                yield break;
            }

            yield return StartCoroutine(InitializeAddressables());
            yield return StartCoroutine(CheckForCatalogUpdates());
            
            // NOTE: With the Fallback logic above, this download step will now be instant (0 MB)
            // for any file that exists in StreamingAssets!
            yield return StartCoroutine(DownloadContentToCache(labelToDownload));

            IsInitialized = true;
            GameDebug.Log("Addressables System Ready.");
        }

        private IEnumerator InitializeAddressables()
        {
            GameDebug.Log("Starting InitializeAddressables...");
            AsyncOperationHandle<IResourceLocator> handle = Addressables.InitializeAsync();
            yield return handle;
            GameDebug.Log("Finished InitializeAddressables.");
        }

        private IEnumerator CheckForCatalogUpdates()
        {
            GameDebug.Log("Checking for Catalog Updates (Remote Config)...");
            AsyncOperationHandle<List<string>> checkHandle = Addressables.CheckForCatalogUpdates();
            yield return checkHandle;

            // CRITICAL FIX: We must check .IsValid() BEFORE checking .Status
            // If the handle is invalid, accessing .Status throws the exception you saw.
            if (checkHandle.IsValid() && checkHandle.Status == AsyncOperationStatus.Succeeded)
            {
                List<string> catalogUpdates = checkHandle.Result;
                if (catalogUpdates.Any())
                {
                    GameDebug.Log($"Updates found ({catalogUpdates.Count} catalogs). Syncing with Remote...");
                    
                    AsyncOperationHandle<List<IResourceLocator>> updateHandle = Addressables.UpdateCatalogs(catalogUpdates);
                    yield return updateHandle;
                    
                    // Same safety rule here
                    if (updateHandle.IsValid())
                    {
                        if (updateHandle.Status == AsyncOperationStatus.Succeeded)
                        {
                            GameDebug.Log("Catalogs updated successfully.");
                        }
                        Addressables.Release(updateHandle);
                    }
                }
                else
                {
                    GameDebug.Log("Catalogs are up to date.");
                }
            }
            else
            {
                // If it's invalid OR failed, we land here safely without crashing
                if (checkHandle.IsValid())
                {
                    GameDebug.LogError($"Catalog Check Failed: {checkHandle.OperationException}");
                }
                else
                {
                    GameDebug.LogWarning("Catalog Check returned an invalid handle (System likely not ready).");
                }
            }

            if (checkHandle.IsValid())
            {
                Addressables.Release(checkHandle);
            }
        }

        private IEnumerator DownloadContentToCache(string label)
        {
            // Because of the Interceptor in Awake, 'GetDownloadSizeAsync' inside here 
            // will return 0 for embedded files, skipping the download loop automatically!
            
            AsyncOperationHandle<IList<IResourceLocation>> locationsHandle;

            if (string.IsNullOrEmpty(label))
            {
                IEnumerable<object> allKeys = Addressables.ResourceLocators.SelectMany(x => x.Keys);
                locationsHandle = Addressables.LoadResourceLocationsAsync(allKeys, Addressables.MergeMode.Union);
            }
            else
            {
                locationsHandle = Addressables.LoadResourceLocationsAsync(label);
            }

            yield return locationsHandle;

            if (!locationsHandle.IsValid() || locationsHandle.Status != AsyncOperationStatus.Succeeded || !locationsHandle.Result.Any())
            {
                if (locationsHandle.IsValid())
                {
                    Addressables.Release(locationsHandle);
                }
                yield break;
            }
            
            IList<IResourceLocation> locations = locationsHandle.Result;

            AsyncOperationHandle<long> sizeHandle = Addressables.GetDownloadSizeAsync(locations);
            yield return sizeHandle;

            long downloadSize = sizeHandle.Result;
            Addressables.Release(sizeHandle);

            bool hasDownload = downloadSize > 0;
            GameDebug.Log($"[SOURCE: {(hasDownload ? "REMOTE" : "LOCAL/EMBEDDED")}] Need to download: {downloadSize / 1024.0 / 1024.0:F2} MB");
            
            if (hasDownload)
            {
                AsyncOperationHandle downloadHandle = Addressables.DownloadDependenciesAsync(locations);
                while (!downloadHandle.IsDone)
                {
                    OnDownloadProgress?.Invoke((long)(downloadSize * downloadHandle.PercentComplete), downloadSize);
                    yield return null;
                }
                if (downloadHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    GameDebug.Log("Download Complete.");
                }
                else
                {
                    GameDebug.LogError($"Download Failed: {downloadHandle.OperationException}");
                }
                
                Addressables.Release(downloadHandle);
            }

            Addressables.Release(locationsHandle);
            OnDownloadComplete?.Invoke();
        }

        private void ClearCacheAndRestart()
        {
             bool success = Caching.ClearCache();
             string addressablesPath = Path.Combine(Application.persistentDataPath, "com.unity.addressables");
             if (Directory.Exists(addressablesPath))
             {
                 Directory.Delete(addressablesPath, true);
             }
             GameDebug.Log($"[Cache] Cleared: {success}.");
        }

        public static void LoadAsset<T>(string key, Action<T> onComplete) where T : Object
        {
            if (activeHandles.TryGetValue(key, out AsyncOperationHandle handle))
            {
                if (handle.IsValid() && handle.IsDone)
                {
                    onComplete?.Invoke(handle.Result as T);
                    return;
                }
            }

            AsyncOperationHandle<T> newHandle = Addressables.LoadAssetAsync<T>(key);
            newHandle.Completed += op => 
            {
                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    if (activeHandles.ContainsKey(key))
                    {
                        activeHandles[key] = newHandle;
                    }
                    else
                    {
                        activeHandles.Add(key, newHandle);
                    }
                    onComplete?.Invoke(op.Result);
                }
                else
                {
                    GameDebug.LogError($"[Error] Failed to load asset: {key}");
                    if (newHandle.IsValid()) Addressables.Release(newHandle);
                    onComplete?.Invoke(null);
                }
            };
        }

        public static void ReleaseAsset(string key)
        {
             if (activeHandles.TryGetValue(key, out AsyncOperationHandle handle))
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
                activeHandles.Remove(key);
            }
        }
    }
}