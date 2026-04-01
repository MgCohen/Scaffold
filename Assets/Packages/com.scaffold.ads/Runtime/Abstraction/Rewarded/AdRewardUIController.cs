using UnityEngine;
using UnityEngine.UI;

namespace Scaffold.Ads
{
    public class AdRewardUIController : MonoBehaviour
    {
        [Header("UI Dependencies")]
        [SerializeField]
        private Button _rewardButton;

        [Header("Ads Settings")]
        [SerializeField]
        private AdManager adManager;

        [SerializeField]
        private string _placementName = "Main_Menu";

        private RewardedAdManager RewardedAds => adManager != null ? adManager.RewardedAds : null;

        private void Awake()
        {
            if (adManager == null)
            {
#if UNITY_2023_1_OR_NEWER
                adManager = FindFirstObjectByType<AdManager>();
#else
                _globalAdManager = FindObjectOfType<GlobalAdManager>();
#endif
            }
        }

        private void Start()
        {
            if (_rewardButton != null)
            {
                _rewardButton.onClick.AddListener(HandleClickAdReward);
            }
        }

        private void OnEnable()
        {
            if (RewardedAds != null)
            {
                RewardedAds.AdSuccessfullyCompleted += HandleAdCompleted;
                RewardedAds.AdAvailable += HandleAdAvailabilityChanged;
            }

            UpdateButtonState();
        }

        private void OnDisable()
        {
            if (RewardedAds != null)
            {
                RewardedAds.AdSuccessfullyCompleted -= HandleAdCompleted;
                RewardedAds.AdAvailable -= HandleAdAvailabilityChanged;
            }

            if (_rewardButton != null)
            {
                _rewardButton.onClick.RemoveListener(HandleClickAdReward);
            }
        }

        private void HandleClickAdReward()
        {
            if (_rewardButton != null)
                _rewardButton.interactable = false;

            if (RewardedAds != null)
            {
                RewardedAds.ClickShowAdReward(_placementName);
                Debug.Log($"Requested ad for placement: {_placementName}");
            }
        }

        private void HandleAdCompleted(bool success, string placementName)
        {
            if (placementName != _placementName) return;

            UpdateButtonState();
            if (success)
            {
                Debug.Log($"Ad completed successfully for placement: {_placementName}");
            }
            else
            {
                Debug.LogWarning($"Ad failed to complete for placement: {_placementName}");
            }
        }

        private void HandleAdAvailabilityChanged(bool isAvailable)
        {
            UpdateButtonState();
            Debug.Log($"Ad availability changed: {(isAvailable ? "available" : "unavailable")}");
        }

        private async void UpdateButtonState()
        {
            if (_rewardButton == null) return;

            if (RewardedAds == null)
            {
                _rewardButton.interactable = false;
                Debug.LogWarning("Ad manager not available");
                return;
            }

            bool isAvailable = await RewardedAds.CanShowAd(_placementName);
            _rewardButton.interactable = isAvailable;

            Debug.Log($"Button state updated for placement '{_placementName}': {(isAvailable ? "enabled" : "disabled")}");

            float cooldownRemaining = RewardedAds.GetRemainingCooldownSeconds();
            if (cooldownRemaining > 0)
            {
                Debug.Log($"Ad on cooldown for {cooldownRemaining:F1} seconds");
            }
        }
    }
}
