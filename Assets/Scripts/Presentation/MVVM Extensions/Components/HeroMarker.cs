using UnityEngine;

namespace Scaffold.MVVM
{
    public class HeroMarker : MonoBehaviour, IHeroHandler
    {
        public string HeroId => heroId;
        [SerializeField] private string heroId;

        private Transform originalParent;
        private Vector3 originalPos;
        private Quaternion originalRot;

        public void SetAnchor()
        {
            originalParent = transform.parent;
            transform.GetLocalPositionAndRotation(out originalPos, out originalRot);
        }

        public void ResetAnchor()
        {
            if(originalParent == null)
            {
                Destroy(gameObject);
                return;
            }

            transform.SetParent(originalParent, true);
            transform.localPosition = originalPos;
            transform.localRotation = originalRot;
        }

        public void DoHeroTransition(IHeroHandler from)
        {
            transform.GetLocalPositionAndRotation(out var targetPos, out var targetRot);

            transform.position = from.transform.position;
            transform.localScale = from.transform.localScale;
        }
    }
}
