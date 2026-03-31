using System;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.Scope.Contracts;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Scope
{
    public abstract class LayeredScope : LifetimeScope
    {
        public bool IsBootstrapCompleted => isBootstrapCompleted;

        [SerializeField]
        private bool isBootstrapCompleted;

        private CancellationTokenSource startupCancellationSource;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<CrossLayerObjectResolver>(Lifetime.Singleton)
                .As<ICrossLayerObjectResolver>()
                .AsSelf();
        }

        private async void Start()
        {
            Debug.Log($"[{GetType().Name}] Starting LayeredScope Bootstrap...");
            CreateStartupCancellation();

            try
            {
                await StartAsync(startupCancellationSource.Token);
                isBootstrapCompleted = true;
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("Bootstrap startup canceled.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        protected Task StartAsync(CancellationToken cancellationToken)
        {
            return RunStartupAsync(cancellationToken);
        }

        private async Task RunStartupAsync(CancellationToken cancellationToken)
        {
            InitializeCrossLayerResolver();

            LayerInstallerBase rootInstaller = BuildLayerTree();
            if (rootInstaller == null)
            {
                throw new InvalidOperationException("Layer tree root cannot be null.");
            }

            rootInstaller.Reset();
            Debug.Log($"[{GetType().Name}] Executing root installer BuildAsRootAsync...");
            ILayeredScopeProgress progressListener = GetLayerProgressListener();
            await rootInstaller.BuildAsRootAsync(this, cancellationToken, progressListener);
            LifetimeScope finalScope = rootInstaller.GetFinalScope();
            Debug.Log($"[{GetType().Name}] Bootstrap BuildAsRootAsync complete, invoking OnBootstrapCompleted...");
            OnBootstrapCompleted(finalScope ?? this);
        }

        private void InitializeCrossLayerResolver()
        {
            if (Container == null)
            {
                return;
            }

            ICrossLayerObjectResolver crossLayerResolver = Container.Resolve<ICrossLayerObjectResolver>();
            crossLayerResolver.Reset();
            crossLayerResolver.RegisterScope(Container);
        }

        protected abstract LayerInstallerBase BuildLayerTree();

        protected abstract void OnBootstrapCompleted(LifetimeScope finalScope);

        protected virtual ILayeredScopeProgress GetLayerProgressListener()
        {
            return GetComponentInChildren<ILayeredScopeProgress>();
        }

        protected override void OnDestroy()
        {
            startupCancellationSource?.Cancel();
            startupCancellationSource?.Dispose();
            startupCancellationSource = null;
            base.OnDestroy();
        }

        private void CreateStartupCancellation()
        {
            startupCancellationSource?.Cancel();
            startupCancellationSource?.Dispose();
            startupCancellationSource = new CancellationTokenSource();
        }
    }
}
