using System;
using System.Threading.Tasks;
using Scaffold.Navigation.Contracts;
using UnityEngine;

namespace Scaffold.Navigation
{
    public class NavigationPoint
    {
        public NavigationPoint(IView view, IViewController controller, ViewConfig config, bool isSceneView, NavigationOptions options, Action<NavigationPoint> disposeAction = null) : this(view, controller, config, isSceneView, options, disposeAction, true)
        {
        }

        internal NavigationPoint(IViewController controller, ViewConfig config, bool isSceneView, NavigationOptions options, Action<NavigationPoint> disposeAction = null) : this(null, controller, config, isSceneView, options, disposeAction, false)
        {
        }

        private NavigationPoint(IView view, IViewController controller, ViewConfig config, bool isSceneView, NavigationOptions options, Action<NavigationPoint> disposeAction, bool ready)
        {
            if (controller is null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            ViewModel = controller;
            Config = config;
            View = view;
            IsSceneView = isSceneView;
            Options = options;
            this.disposeAction = disposeAction;
            if (ready)
            {
                readyCompletion.TrySetResult(true);
            }
        }

        public IView View { get; internal set; }
        public IViewController ViewModel { get; private set; }
        public ViewConfig Config { get; private set; }
        public bool IsSceneView { get; private set; }
        public int Depth { get; private set; }
        public NavigationOptions Options { get; private set; }
        public bool Disposed { get; private set; }
        public bool IsReady => View != null;

        private readonly Action<NavigationPoint> disposeAction;
        private readonly TaskCompletionSource<bool> readyCompletion = new TaskCompletionSource<bool>();

        public void SetDepth(int depth, NavigationOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            Depth = depth;
            if (View == null)
            {
                return;
            }

            ApplyDepth(options);
        }

        internal void CompleteReady(IView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (Disposed)
            {
                return;
            }

            View = view;
            ApplyDepth(Options);
            readyCompletion.TrySetResult(true);
        }

        private void ApplyDepth(NavigationOptions options)
        {
            View.Order(Depth);
            if (options.RenderOverride.HasValue)
            {
                ApplyRenderOverride(options.RenderOverride.Value);
            }
        }

        private void ApplyRenderOverride(RenderMode renderMode)
        {
            Canvas canvas = View.gameObject.GetComponentInParent<Canvas>(true);
            if (canvas == null)
            {
                return;
            }

            canvas.renderMode = renderMode;
        }

        internal Task AwaitReadyAsync()
        {
            return readyCompletion.Task;
        }

        internal void FailReady(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            readyCompletion.TrySetException(exception);
        }

        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }

            disposeAction?.Invoke(this);
            View = null;
            ViewModel = null;
            Config = null;
            Disposed = true;
            readyCompletion.TrySetResult(true);
        }
    }
}
