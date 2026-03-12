using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Ads.Managers;
using Game.Ads.Configurations;

namespace Game.Ads.Test
{
    public class RewardedAdTester : MonoBehaviour
    {
        [Header("Ad System")]
        public AdManager adManager;

        [Header("Rewarded Ads Config")]
        public List<RewardedAdPlacementUI> placements = new();

        private void Start()
        {
            foreach (RewardedAdPlacementUI placement in placements)
            {
                if (placement.buttonShow != null)
                {
                    placement.buttonShow.onClick.AddListener(() => ShowRewardedAd(placement.key));
                }

                if (placement.buttonFetch != null)
                {
                    placement.buttonFetch.onClick.AddListener(() => FetchAdStatus(placement));
                }
            }
        }

        private void Update()
        {
            if (adManager == null || adManager.RewardedAds == null)
            {
                return;
            }

            foreach (RewardedAdPlacementUI placement in placements)
            {
                UpdatePlacementUI(placement);
            }
        }

        private void UpdatePlacementUI(RewardedAdPlacementUI entry)
        {
            if (entry.key == null) return;

            // Update Cooldown Text
            if (entry.cooldownText != null)
            {
                float remaining = adManager.RewardedAds.GetRemainingCooldownSeconds(entry.key);
                if (remaining > 0)
                {
                    TimeSpan t = TimeSpan.FromSeconds(remaining);
                    entry.cooldownText.text = $"Cooldown: {t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
                }
                else
                {
                    entry.cooldownText.text = "Cooldown: Ready";
                }
            }

            // Update Status Text and Button Interactivity
            bool onCooldown = adManager.RewardedAds.GetRemainingCooldownSeconds(entry.key) > 0;
            bool canShow = entry.isAdAvailable && !onCooldown;

            if (entry.statusText != null)
            {
                if (onCooldown)
                {
                    entry.statusText.text = "<color=orange>COOLDOWN</color>";
                }
                else
                {
                    entry.statusText.text = entry.isAdAvailable ? "<color=green>READY</color>" : "<color=red>UNAVAILABLE</color>";
                }
            }

            if (entry.buttonShow != null)
            {
                entry.buttonShow.interactable = canShow;
            }

            if (entry.buttonFetch != null)
            {
                entry.buttonFetch.interactable = !entry.isFetching && !entry.isAdAvailable;
            }
        }

        private async void FetchAdStatus(RewardedAdPlacementUI entry)
        {
            if (adManager == null || adManager.RewardedAds == null || entry.key == null)
            {
                return;
            }

            entry.isFetching = true;
            try
            {
                entry.isAdAvailable = await adManager.RewardedAds.CanShowAd(entry.key);
            }
            finally
            {
                entry.isFetching = false;
            }
        }

        private void OnDestroy()
        {
            foreach (var placement in placements)
            {
                if (placement.buttonShow != null)
                {
                    placement.buttonShow.onClick.RemoveAllListeners();
                }
                if (placement.buttonFetch != null)
                {
                    placement.buttonFetch.onClick.RemoveAllListeners();
                }
            }
        }

        public void ShowRewardedAd(string placement)
        {
            Debug.Log($"Test.ShowRewardedAd [{placement}] via GlobalAdManager");
            if (adManager != null && adManager.RewardedAds != null)
            {
                adManager.RewardedAds.ClickShowAdReward(placement);
            }
        }

        private void OnValidate()
        {
            if (placements == null) return;

            HashSet<AdPlacementKeySO> seenKeys = new HashSet<AdPlacementKeySO>();
            for (int i = 0; i < placements.Count; i++)
            {
                if (placements[i].key != null)
                {
                    if (seenKeys.Contains(placements[i].key))
                    {
                        Debug.LogWarning($"Duplicate AdPlacementKeySO found: {placements[i].key.name}. Please use unique keys.");
                        placements[i].key = null;
                    }
                    else
                    {
                        seenKeys.Add(placements[i].key);
                    }
                }
            }
        }
    }
}