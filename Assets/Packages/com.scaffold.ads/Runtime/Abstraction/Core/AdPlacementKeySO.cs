using UnityEngine;

namespace Scaffold.Ads
{
    [CreateAssetMenu(fileName = "NewAdPlacementKey", menuName = "Ads/Ad Placement Key")]
    public class AdPlacementKeySO : ScriptableObject
    {
        [Tooltip("The actual placement string passed to the SDKs (e.g. 'Test_Placement').")]
        public string Id;

        public override string ToString()
        {
            return Id;
        }

        public static implicit operator string(AdPlacementKeySO keySO)
        {
            return keySO != null ? keySO.Id : string.Empty;
        }
    }
}
