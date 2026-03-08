using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Ads.Managers;
using Game.Ads.Configurations;

namespace Game.Ads.Test
{
    public class RewardedAdTester : MonoBehaviour
    {
        [Header("Ad System")]
        public AdManager adManager;

        [Header("Rewarded Ads")]
        public AdPlacementKeySO rewardedDefaultKey;
        public AdPlacementKeySO rewardedAltKey;
        public Button btnShowRewardedDefault;
        public Button btnShowRewardedAlt;
        public TextMeshProUGUI cooldownText;
        public TextMeshProUGUI statusText;

        private void Start()
        {
            if (btnShowRewardedDefault != null) btnShowRewardedDefault.onClick.AddListener(() => ShowRewardedAd(rewardedDefaultKey));
            if (btnShowRewardedAlt != null) btnShowRewardedAlt.onClick.AddListener(() => ShowRewardedAd(rewardedAltKey));
        }

        public async void Update()
        {
            if (adManager == null || adManager.RewardedAds == null) return;

            if (cooldownText != null)
            {
                float remaining = adManager.RewardedAds.GetRemainingCooldownSeconds();
                if (remaining > 0)
                {
                    TimeSpan t = TimeSpan.FromSeconds(remaining);
                    cooldownText.text = $"Cooldown: {t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
                }
                else
                {
                    cooldownText.text = "Cooldown: Ready";
                }
            }

            if (statusText != null)
            {
                bool ready = await adManager.RewardedAds.CanShowAd();
                statusText.text = ready ? "<color=green>READY</color>" : "<color=red>WAITING</color>";
            }
        }

        private void OnDestroy()
        {
            if (btnShowRewardedDefault != null) btnShowRewardedDefault.onClick.RemoveAllListeners();
            if (btnShowRewardedAlt != null) btnShowRewardedAlt.onClick.RemoveAllListeners();
        }

        public void ShowRewardedAd(string placement)
        {
            Debug.Log($"Test.ShowRewardedAd [{placement}] via new GlobalAdManager");
            if (adManager != null && adManager.RewardedAds != null)
            {
                adManager.RewardedAds.ClickShowAdReward(placement);
            }
        }
    }
}
