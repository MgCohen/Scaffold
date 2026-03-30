using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Scaffold.Addressable;
using Scaffold.Logging;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Scaffold.UI
{
    /// <summary>
    /// Manages the Loading Screen UI, listening to the AddressableController events.
    /// Handles the visual transition from Downloading Assets -> Loading Scene.
    /// </summary>
    public class AddressablesUI : MonoBehaviour
    {
        #region Configuration
        private const float BYTES_TO_MB = 1048576f; // 1024 * 1024

        [Header("References")]
        [Tooltip("Reference to the persistent AddressableController.")]
        [SerializeField] 
        private AddressableController controller;

        [Header("UI Elements")]
        [SerializeField] 
        private GameObject uiPanel;
        
        [SerializeField] 
        private Slider progressBar;
        
        [SerializeField] 
        private TextMeshProUGUI statusText;
        
        [SerializeField] 
        private TextMeshProUGUI sizeText;

        [Header("Settings")]
        [Tooltip("Minimum visual value for the slider so it doesn't look empty.")]
        [SerializeField] 
        private float minProgressValue = 0.035f;
        #endregion

        #region Unity Lifecycle
        private void Start()
        {
            if (controller == null)
            {
                GameDebug.LogError("[AddressablesUI] Controller reference is missing!");
                return;
            }

            // Subscribe to controller events
            controller.OnDownloadProgress += UpdateDownloadUI;
            controller.OnDownloadComplete += HandleDownloadComplete;

            // Initialize UI State
            if (uiPanel)
            {
                uiPanel.SetActive(true);
            }

            if (progressBar)
            {
                progressBar.value = minProgressValue;
            }
            
            UpdateText("Checking for updates", "");
        }

        private void OnDestroy()
        {
            // Unsubscribe to prevent memory leaks if this UI is destroyed before the controller
            if (controller == null)
            {
                return;
            }

            controller.OnDownloadProgress -= UpdateDownloadUI;
            controller.OnDownloadComplete -= HandleDownloadComplete;
        }
        #endregion

        #region UI Updates
        /// <summary>
        /// Updates the visual slider, clamping it to the minimum visual value.
        /// </summary>
        private void UpdateProgressSlider(float progress)
        {
            if (progressBar)
            {
                progressBar.value = Mathf.Max(minProgressValue, progress);
            }
        }

        /// <summary>
        /// Event Handler: Called by AddressableController when downloading bundles.
        /// </summary>
        private void UpdateDownloadUI(long currentBytes, long totalBytes)
        {
            // Avoid divide by zero
            if (totalBytes <= 0)
            {
                return;
            }

            float progress = (float)currentBytes / totalBytes;
            UpdateProgressSlider(progress);

            // Calculate MB
            float currentMB = currentBytes / BYTES_TO_MB;
            float totalMB = totalBytes / BYTES_TO_MB;

            // F2 formats to 2 decimal places (e.g. "12.50")
            UpdateText("Downloading content", $"{currentMB:F2} MB / {totalMB:F2} MB");
        }

        /// <summary>
        /// Updates text specifically for the Scene Loading phase.
        /// </summary>
        private void UpdateLoadingSceneUI(float progress)
        {
            UpdateProgressSlider(progress);
            UpdateText("Loading", $"{progress * 100:F0}%");
        }

        private void UpdateText(string status, string size)
        {
            if (statusText)
            {
                statusText.text = status;
            }
            if (sizeText)
            {
                sizeText.text = size;
            }
        }
        #endregion

        #region Logic Flow
        /// <summary>
        /// Event Handler: Called when bundle downloads are finished (or skipped if unnecessary).
        /// Starts the Scene loading process.
        /// </summary>
        private async void HandleDownloadComplete()
        {
            // 1. Visually fill the bar for the "Download" phase
            if (progressBar)
            {
                progressBar.value = 1f;
            }
            UpdateText("Download Finished", "Content Ready");

            // 2. Validate Scene Reference
            if (controller.sceneAssetReference == null || !controller.sceneAssetReference.RuntimeKeyIsValid())
            {
                GameDebug.LogWarning("[AddressablesUI] No valid scene reference assigned to load.");
                if (uiPanel)
                {
                    uiPanel.SetActive(false);
                }
                return;
            }

            GameDebug.Log("[AddressablesUI] Starting Scene Load...");

            // 3. Start Loading the Scene
            // NOTE: LoadSceneMode.Single closes the current scene (where this UI might be).
            // If this UI is meant to persist, ensure it is DontDestroyOnLoad or the new scene has its own UI.
            AsyncOperationHandle<SceneInstance> sceneHandle = controller.sceneAssetReference.LoadSceneAsync(LoadSceneMode.Single);

            // 4. Optional: Artificial delay for UX (so the "Finished" text is readable for a moment)
            // Use Random.Range to make it feel organic, but keep it short.
            //await Task.Delay(Random.Range(500, 1000)); 
            
            // 5. Monitor Progress
            // !Important! We loop while !IsDone, awaiting Task.Yield() to let Unity render the frame.
            // Without Task.Yield(), this loop would block the main thread and freeze the UI.
            while (!sceneHandle.IsDone)
            {
                UpdateLoadingSceneUI(sceneHandle.PercentComplete);
                await Task.Yield(); 
            }

            // 6. Finalize
            if (sceneHandle.Status == AsyncOperationStatus.Succeeded)
            {
                GameDebug.Log("[AddressablesUI] Scene Loaded Successfully.");
                // Ensure UI is hidden if this object persists
                if (uiPanel)
                {
                    uiPanel.SetActive(false);
                }
            }
            else
            {
                GameDebug.LogError($"[AddressablesUI] Scene load failed: {sceneHandle.OperationException}");
                UpdateText("Error Loading Scene", "Please Restart");
            }
        }
        #endregion
    }
}