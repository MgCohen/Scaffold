using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Scaffold.Ads
{
    public class AdRewardUIController : MonoBehaviour
    {
        private RewardedAdManager RewardedAds => adManager != null ? adManager.RewardedAds : null;

        [Header("UI Dependencies")]
        [SerializeField]
        private Button rewardButton;

        [Inject]
        private AdManager adManager;

        [SerializeField]
        private string placementName = "Main_Menu";

        private void Start()
        {
            if (rewardButton != null)
            {
                rewardButton.onClick.AddListener(HandleClickAdReward);
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

            if (rewardButton != null)
            {
                rewardButton.onClick.RemoveListener(HandleClickAdReward);
            }
        }

        private void HandleClickAdReward()
        {
            if (rewardButton != null)
            {
                rewardButton.interactable = false;
            }

            if (RewardedAds != null)
            {
                RewardedAds.ClickShowAdReward(placementName);
                Debug.Log($"Requested ad for placement: {placementName}");
            }
        }

        private void HandleAdCompleted(bool success, string placement)
        {
            if (placement != placementName)
            {
                return;
            }

            UpdateButtonState();
            if (success)
            {
                Debug.Log($"Ad completed successfully for placement: {placementName}");
            }
            else
            {
                Debug.LogWarning($"Ad failed to complete for placement: {placementName}");
            }
        }

        private void HandleAdAvailabilityChanged(bool isAvailable)
        {
            UpdateButtonState();
            Debug.Log($"Ad availability changed: {(isAvailable ? "available" : "unavailable")}");
        }

        private async void UpdateButtonState()
        {
            if (rewardButton == null)
            {
                return;
            }

            if (RewardedAds == null)
            {
                rewardButton.interactable = false;
                Debug.LogWarning("Ad manager not available");
                return;
            }

            bool isAvailable = await RewardedAds.CanShowAd(placementName);
            rewardButton.interactable = isAvailable;
            LogButtonState(placementName, isAvailable);
            LogCooldownIfNeeded();
        }

        private void LogButtonState(string placement, bool isAvailable)
        {
            Debug.Log($"Button state updated for placement '{placement}': {(isAvailable ? "enabled" : "disabled")}");
        }

        private void LogCooldownIfNeeded()
        {
            float cooldownRemaining = RewardedAds.GetRemainingCooldownSeconds();
            if (cooldownRemaining > 0)
            {
                Debug.Log($"Ad on cooldown for {cooldownRemaining:F1} seconds");
            }
        }
    }
}
