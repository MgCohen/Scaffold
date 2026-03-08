using UnityEngine;
using UnityEngine.UI;
using Game.Ads.Managers;
using Game.Ads.Configurations;

namespace Game.Ads.Test
{
    public class InterstitialAdTester : MonoBehaviour
    {
        [Header("Ad System")]
        public AdManager adManager;

        [Header("Interstitial Ads")]
        public AdPlacementKeySO interstitialDefaultKey;
        public AdPlacementKeySO interstitialAltKey;
        public Button btnShowInterstitialDefault;
        public Button btnShowInterstitialAlt;

        private void Start()
        {
            if (btnShowInterstitialDefault != null)
            {
                btnShowInterstitialDefault.onClick.AddListener(() => ShowInterstitialAd(interstitialDefaultKey));
            }
            if (btnShowInterstitialAlt != null)
            {
                btnShowInterstitialAlt.onClick.AddListener(() => ShowInterstitialAd(interstitialAltKey));
            }
        }

        private void OnDestroy()
        {
            if (btnShowInterstitialDefault != null)
            {
                btnShowInterstitialDefault.onClick.RemoveAllListeners();
            }
            if (btnShowInterstitialAlt != null)
            {
                btnShowInterstitialAlt.onClick.RemoveAllListeners();
            }
        }

        public void ShowInterstitialAd(string placement)
        {
            Debug.Log($"Test.ShowInterstitialAd [{placement}] via new GlobalAdManager");
            if (adManager != null && adManager.InterstitialAds != null)
            {
                adManager.InterstitialAds.ShowInterstitial(placement);
            }
        }
    }
}
