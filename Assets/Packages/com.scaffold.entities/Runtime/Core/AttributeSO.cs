using UnityEngine;

namespace Scaffold.Entities
{
    /// <summary>
    /// ScriptableObject identity for an attribute slot; defaults and editor authoring. Use reference-based
    /// resolution first; implicit conversion supplies an <see cref="Attribute"/> with <see cref="Attribute.MatchKey"/> = asset name.
    /// </summary>
    [CreateAssetMenu(menuName = "Scaffold/Entity/Attribute", fileName = "Attribute")]
    public class AttributeSO : ScriptableObject
    {
        public string DefaultPayload => defaultPayload ?? string.Empty;

        [SerializeField]
        private string defaultPayload = string.Empty;

        public static implicit operator Attribute(AttributeSO so)
        {
            if (so == null)
            {
                return default;
            }

            return new Attribute(so.DefaultPayload, so.name);
        }
    }
}
