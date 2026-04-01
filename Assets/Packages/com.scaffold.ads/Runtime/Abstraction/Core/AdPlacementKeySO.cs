using UnityEngine;

namespace Scaffold.Ads
{
    [CreateAssetMenu(fileName = "NewAdPlacementKey", menuName = "Ads/Ad Placement Key")]
    public class AdPlacementKeySO : ScriptableObject
    {
        [Tooltip("The actual placement string passed to the SDKs (e.g. 'Test_Placement').")]
        public string id;

        // Allows standard string methods (like mapping/comparing) to receive 
        // this ScriptableObject instance and automatically extract the string id.
        public static implicit operator string(AdPlacementKeySO keySO)
        {
            return keySO != null ? keySO.id : string.Empty;
        }
        
        // Ensure proper stringification
        public override string ToString()
        {
            return id;
        }
    }
}
