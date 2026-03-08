
using UnityEngine;
using UnityEngine.UI;

namespace Scaffold.Navigation
{
    public class NavigationPoint
    {
        public NavigationPoint(IView view, IViewController controller, ViewConfig config, bool isSceneView, NavigationOptions options)
        {
            View = view;
            ViewModel = controller;
            Config = config;
            IsSceneView = isSceneView;
            Options = options;
        }

        public IView View { get; private set; }
        public IViewController ViewModel { get; private set; }
        public ViewConfig Config { get; private set; }
        public bool IsSceneView { get; private set; }
        public int Depth { get; private set; }
        public NavigationOptions Options { get; private set; }

        public bool Disposed { get; private set; }

        public void SetDepth(int depth, NavigationOptions options)
        {
            Depth = depth;
            View.Order(depth);
            if (options.RenderOverride.HasValue)
            {
                ApplyRenderOverride(options.RenderOverride.Value);
            }
        }

        private void ApplyRenderOverride(RenderMode renderMode)
        {
            var canvas = View.gameObject.GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                canvas.renderMode = renderMode;
            }
        }

        public void Dispose()
        {
            View = null;
            ViewModel = null;
            Config = null;
            Disposed = true;
        }
    }
}
