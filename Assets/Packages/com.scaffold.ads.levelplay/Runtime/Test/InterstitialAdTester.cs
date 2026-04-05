using UnityEngine;
using UnityEngine.UI;

namespace Scaffold.Ads.Levelplay.Test
{
    public class InterstitialAdTester : MonoBehaviour
    {
        [Header("Ad System")]
        public AdManager AdManager;

        [Header("Interstitial Ads")]
        public AdPlacementKeySO InterstitialDefaultKey;
        public AdPlacementKeySO InterstitialAltKey;
        public Button BtnShowInterstitialDefault;
        public Button BtnShowInterstitialAlt;

        private void Start()
        {
            if (BtnShowInterstitialDefault != null)
            {
                BtnShowInterstitialDefault.onClick.AddListener(() => ShowInterstitialAd(InterstitialDefaultKey));
            }
            if (BtnShowInterstitialAlt != null)
            {
                BtnShowInterstitialAlt.onClick.AddListener(() => ShowInterstitialAd(InterstitialAltKey));
            }
        }

        private void OnDestroy()
        {
            if (BtnShowInterstitialDefault != null)
            {
                BtnShowInterstitialDefault.onClick.RemoveAllListeners();
            }
            if (BtnShowInterstitialAlt != null)
            {
                BtnShowInterstitialAlt.onClick.RemoveAllListeners();
            }
        }

        public void ShowInterstitialAd(string placement)
        {
            Debug.Log($"Test.ShowInterstitialAd [{placement}] via new GlobalAdManager");
            if (AdManager != null && AdManager.InterstitialAds != null)
            {
                AdManager.InterstitialAds.ShowInterstitial(placement);
            }
        }
    }
}
