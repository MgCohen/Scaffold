using UnityEngine;
using UnityEngine.UI;

namespace Scaffold.SceneFlow
{
    /// <summary>
    /// Standalone transition loading UI. Callers (e.g. a flow that loads Addressable scenes) own when to show or hide it; it is not registered in DI or coupled to <see cref="SceneFlowService"/>.
    /// </summary>
    public sealed class LoadingView : MonoBehaviour
    {
        public bool IsVisible => isVisible;

        [SerializeField] private bool isVisible;

        [SerializeField] private GameObject presenterRoot;

        [SerializeField] private Image progressFill;

        private void Awake()
        {
            if (presenterRoot == null)
            {
                EnsureDefaultUi();
            }

            Hide();
        }

        public void Show()
        {
            if (presenterRoot != null)
            {
                presenterRoot.SetActive(true);
            }

            isVisible = true;
        }

        public void Hide()
        {
            if (presenterRoot != null)
            {
                presenterRoot.SetActive(false);
            }

            isVisible = false;
        }

        public void SetProgress(float normalized)
        {
            if (progressFill == null)
            {
                return;
            }

            progressFill.fillAmount = Mathf.Clamp01(normalized);
        }

        private void EnsureDefaultUi()
        {
            GameObject root = CreatePresenterRoot();
            presenterRoot = root;
            ConfigurePresenterLayout(root);
        }

        private void ConfigurePresenterLayout(GameObject root)
        {
            root.transform.SetParent(transform, false);
            RectTransform rootRt = root.AddComponent<RectTransform>();
            StretchRectToFullScreen(rootRt);
            BuildCanvasStack(root);
            CreateDimmer(root.transform);
            CreateProgressBar(root.transform);
        }

        private void BuildCanvasStack(GameObject root)
        {
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1500;
            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            root.AddComponent<GraphicRaycaster>();
        }

        private void CreateProgressBar(Transform parent)
        {
            GameObject bar = new GameObject("ProgressBar");
            bar.transform.SetParent(parent, false);
            RectTransform barRt = bar.AddComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0.15f, 0.46f);
            barRt.anchorMax = new Vector2(0.85f, 0.48f);
            barRt.offsetMin = Vector2.zero;
            barRt.offsetMax = Vector2.zero;
            Image bg = bar.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.18f, 1f);
            CreateFill(bar.transform);
        }

        private void CreateFill(Transform barTransform)
        {
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(barTransform, false);
            progressFill = fill.AddComponent<Image>();
            progressFill.color = new Color(0.25f, 0.65f, 0.95f, 1f);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillAmount = 0f;
            RectTransform fillRt = progressFill.rectTransform;
            StretchRectToFullScreen(fillRt);
        }

        private void CreateDimmer(Transform parent)
        {
            GameObject dim = new GameObject("Dim");
            dim.transform.SetParent(parent, false);
            Image dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0.02f, 0.02f, 0.05f, 0.94f);
            RectTransform dimRt = dim.GetComponent<RectTransform>();
            StretchRectToFullScreen(dimRt);
        }

        private void StretchRectToFullScreen(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static GameObject CreatePresenterRoot()
        {
            return new GameObject("LoadingViewRoot");
        }
    }
}
