using UnityEngine;

namespace Scaffold.MVVM
{
    /// <summary>
    /// Drop-in vertical sine float for any <see cref="RectTransform"/>. Add to the same GameObject as the UI element (or assign <see cref="floatTarget"/>).
    /// </summary>
    [AddComponentMenu("UI/Cleanup/Ui Float")]
    [DisallowMultipleComponent]
    public sealed class UiFloatBehaviour : MonoBehaviour
    {
        [SerializeField] private float floatDistance = 8f;
        [SerializeField] private float cycleDuration = 1.8f;
        [SerializeField] private bool useUnscaledTime = true;

        private Vector2 baseAnchoredPosition;
        private float startTime;
        private bool hasStartTime;

        private void OnEnable()
        {
            RectTransform target = transform as RectTransform;
            if (target == null)
            {
                return;
            }

            baseAnchoredPosition = target.anchoredPosition;
            startTime = useUnscaledTime ? Time.unscaledTime : Time.time;
            hasStartTime = true;
        }

        private void LateUpdate()
        {
            RectTransform target = transform as RectTransform;
            if (target == null || !hasStartTime || floatDistance <= 0f || cycleDuration <= 0f)
            {
                return;
            }

            float now = useUnscaledTime ? Time.unscaledTime : Time.time;
            float elapsed = now - startTime;
            float phase = (elapsed / cycleDuration) * Mathf.PI * 2f;
            float offsetY = Mathf.Sin(phase) * floatDistance;
            target.anchoredPosition = baseAnchoredPosition + new Vector2(0f, offsetY);
        }

        private void OnDisable()
        {
            RectTransform target = transform as RectTransform;
            if (target != null)
            {
                target.anchoredPosition = baseAnchoredPosition;
            }

            hasStartTime = false;
        }
    }
}
