using System;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using Scaffold.Logging;
using UnityEngine;

namespace Scaffold.Shared
{
    [Serializable]
    public class AssetReferenceScene : AssetReference
    {
        /// <summary>
        /// Constructs a new reference to a Scene.
        /// </summary>
        /// <param name="guid">The object guid.</param>
        public AssetReferenceScene(string guid) : base(guid)
        {
        }

        /// <summary>
        /// Optional: This ensures only Scene assets can be dragged into this field in the Editor.
        /// </summary>
        public override bool ValidateAsset(UnityEngine.Object obj)
        {
#if UNITY_EDITOR
            return obj is UnityEditor.SceneAsset;
#else
            return base.ValidateAsset(obj);
#endif
        }
        
        public async Awaitable LoadSceneAsync(LoadSceneMode mode, bool activateOnLoad, bool setActiveScene, Action<SceneInstance> onComplete = null)
        {
            await LoadSceneAsync(RuntimeKey.ToString(), mode, activateOnLoad, setActiveScene, onComplete);
        }
        
        public static async Awaitable LoadSceneAsync(string key, LoadSceneMode mode, bool activateOnLoad, bool setActiveScene, Action<SceneInstance> onComplete = null)
        {
            GameDebug.Log($"[Scene] Requesting Load: {key}");
            AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(key, mode, activateOnLoad);
            try
            {
                await handle.Task;
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    if (setActiveScene)
                    {
                        SceneManager.SetActiveScene(handle.Result.Scene);
                    }
                    GameDebug.Log($"[Scene] Successfully Loaded: {key}, active scene: {setActiveScene}");
                    onComplete?.Invoke(handle.Result);
                }
                else
                {
                    GameDebug.LogError($"[Error] Failed to load scene '{key}': {handle.OperationException}");
                    if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }
                }
            }
            catch (Exception e)
            {
                GameDebug.LogError($"[Error] Failed to load scene '{key}': {e.Message}");
                // If the handle is valid but failed, we should release it to clean up memory
                if (handle.IsValid()) 
                {
                    Addressables.Release(handle);
                }
            }
        }
    }
}