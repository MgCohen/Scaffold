using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Ads.Levelplay.Test
{
    public class RewardedAdTester : MonoBehaviour
    {
        [Header("Ad System")]
        public AdManager AdManager;

        [Header("Rewarded Ads Config")]
        public List<RewardedAdPlacementUI> Placements = new();

        private void Start()
        {
            foreach (RewardedAdPlacementUI placement in Placements)
            {
                if (placement.ButtonShow != null)
                {
                    placement.ButtonShow.onClick.AddListener(() => ShowRewardedAd(placement.Key));
                }

                if (placement.ButtonFetch != null)
                {
                    placement.ButtonFetch.onClick.AddListener(() => FetchAdStatus(placement));
                }
            }
        }

        private void Update()
        {
            if (AdManager == null || AdManager.RewardedAds == null)
            {
                return;
            }

            foreach (RewardedAdPlacementUI placement in Placements)
            {
                UpdatePlacementUI(placement);
            }
        }

        private void UpdatePlacementUI(RewardedAdPlacementUI entry)
        {
            if (entry.Key == null)
            {
                return;
            }

            UpdateCooldownLabel(entry);
            UpdateStatusAndButtons(entry);
        }

        private void UpdateCooldownLabel(RewardedAdPlacementUI entry)
        {
            if (entry.CooldownText == null)
            {
                return;
            }

            float remaining = AdManager.RewardedAds.GetRemainingCooldownSeconds(entry.Key);
            if (remaining > 0)
            {
                TimeSpan t = TimeSpan.FromSeconds(remaining);
                entry.CooldownText.text = $"Cooldown: {t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
            }
            else
            {
                entry.CooldownText.text = "Cooldown: Ready";
            }
        }

        private void UpdateStatusAndButtons(RewardedAdPlacementUI entry)
        {
            bool onCooldown = AdManager.RewardedAds.GetRemainingCooldownSeconds(entry.Key) > 0;
            bool canShow = entry.IsAdAvailable && !onCooldown;
            ApplyStatusText(entry, onCooldown);
            ApplyButtonInteractable(entry, canShow);
        }

        private void ApplyStatusText(RewardedAdPlacementUI entry, bool onCooldown)
        {
            if (entry.StatusText == null)
            {
                return;
            }

            if (onCooldown)
            {
                entry.StatusText.text = "<color=orange>COOLDOWN</color>";
            }
            else
            {
                entry.StatusText.text = entry.IsAdAvailable ? "<color=green>READY</color>" : "<color=red>UNAVAILABLE</color>";
            }
        }

        private void ApplyButtonInteractable(RewardedAdPlacementUI entry, bool canShow)
        {
            if (entry.ButtonShow != null)
            {
                entry.ButtonShow.interactable = canShow;
            }

            if (entry.ButtonFetch != null)
            {
                entry.ButtonFetch.interactable = !entry.IsFetching && !entry.IsAdAvailable;
            }
        }

        private async void FetchAdStatus(RewardedAdPlacementUI entry)
        {
            if (AdManager == null || AdManager.RewardedAds == null || entry.Key == null)
            {
                return;
            }

            entry.IsFetching = true;
            try
            {
                entry.IsAdAvailable = await AdManager.RewardedAds.CanShowAd(entry.Key);
            }
            finally
            {
                entry.IsFetching = false;
            }
        }

        private void OnDestroy()
        {
            foreach (RewardedAdPlacementUI placement in Placements)
            {
                if (placement.ButtonShow != null)
                {
                    placement.ButtonShow.onClick.RemoveAllListeners();
                }
                if (placement.ButtonFetch != null)
                {
                    placement.ButtonFetch.onClick.RemoveAllListeners();
                }
            }
        }

        public void ShowRewardedAd(string placement)
        {
            Debug.Log($"Test.ShowRewardedAd [{placement}] via GlobalAdManager");
            if (AdManager != null && AdManager.RewardedAds != null)
            {
                AdManager.RewardedAds.ClickShowAdReward(placement);
            }
        }

        private void OnValidate()
        {
            if (Placements == null)
            {
                return;
            }

            DeduplicatePlacementKeys();
        }

        private void DeduplicatePlacementKeys()
        {
            HashSet<AdPlacementKeySO> seenKeys = new HashSet<AdPlacementKeySO>();
            for (int i = 0; i < Placements.Count; i++)
            {
                DeduplicatePlacementKeyAtIndex(seenKeys, i);
            }
        }

        private void DeduplicatePlacementKeyAtIndex(HashSet<AdPlacementKeySO> seenKeys, int index)
        {
            if (Placements[index].Key == null)
            {
                return;
            }

            if (seenKeys.Contains(Placements[index].Key))
            {
                Debug.LogWarning($"Duplicate AdPlacementKeySO found: {Placements[index].Key.name}. Please use unique keys.");
                Placements[index].Key = null;
            }
            else
            {
                seenKeys.Add(Placements[index].Key);
            }
        }
    }
}
