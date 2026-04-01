using Scaffold.Scope.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Scaffold.Bootstrap
{
    /// <summary>
    /// Full-screen bootstrap loading presentation driven by <see cref="ILayeredScopeProgress"/>.
    /// Assign <see cref="progressSlider"/> and <see cref="loaderRoot"/> from the prefab or scene; this type does not build UI at runtime.
    /// </summary>
    public sealed class BootstrapLoadingView : MonoBehaviour, ILayeredScopeProgress
    {
        [SerializeField] private Slider progressSlider;
        [SerializeField] private GameObject loaderRoot;

        private float pendingNormalized;
        private bool pendingDirty;

        void ILayeredScopeProgress.OnLayerPipelineStep(int completedLayerIndex, int totalLayers)
        {
            if (totalLayers <= 0)
            {
                return;
            }

            pendingNormalized = completedLayerIndex / (float)totalLayers;
            pendingDirty = true;
        }

        private void LateUpdate()
        {
            if (!pendingDirty || progressSlider == null)
            {
                return;
            }

            pendingDirty = false;
            progressSlider.normalizedValue = pendingNormalized;
        }

        public void Hide()
        {
            if (loaderRoot != null)
            {
                loaderRoot.SetActive(false);
                return;
            }

            if (progressSlider != null)
            {
                Canvas canvas = progressSlider.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    canvas.gameObject.SetActive(false);
                    return;
                }
            }

            gameObject.SetActive(false);
        }
    }
}
