using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.Scope.Contracts;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Scope
{
    /// <summary>
    /// Orchestrates two-scope startup (base scope → async init → preload → main child scope → async init).
    /// Per-project composition (Addressables, preloads, app installers) belongs in subclasses, typically in a
    /// game assembly—not in Scaffold.Scope.
    /// </summary>
    public abstract class TwoScopeApplicationHost : LifetimeScope
    {
        public bool IsStartupCompleted => startupCompleted;

        [SerializeField]
        private bool startupCompleted;

        /// <summary>Phase progress for optional loading UI; subscribe to <see cref="ApplicationStartupProgress.Changed"/> or override <see cref="GetStartupProgressListener"/>.</summary>
        protected ApplicationStartupProgress StartupProgress { get; } = new ApplicationStartupProgress();

        private CancellationTokenSource startupCancellationSource;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<CrossLayerObjectResolver>(Lifetime.Singleton)
                .As<ICrossLayerObjectResolver>()
                .AsSelf();
        }

        private async void Start()
        {
            Debug.Log($"[{GetType().Name}] Starting two-scope startup...");
            CreateStartupCancellation();

            try
            {
                await StartAsync(startupCancellationSource.Token);
                startupCompleted = true;
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("Startup canceled.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        protected Task StartAsync(CancellationToken cancellationToken)
        {
            return RunTwoScopeStartupAsync(cancellationToken);
        }

        private async Task RunTwoScopeStartupAsync(CancellationToken cancellationToken)
        {
            InitializeCrossLayerResolver();
            IApplicationStartupProgress progressListener = GetStartupProgressListener();
            LifetimeScope baseScope = await CreateAndRunBaseScopeAsync(cancellationToken);
            await PrepareMainScopeAsync(baseScope.Container, cancellationToken);
            progressListener?.OnStartupPhaseStep(1, 2);
            LifetimeScope mainScope = await CreateAndRunMainScopeAsync(baseScope, cancellationToken);
            await RunLegacyIAsyncLayerInitializersAsync(mainScope.Container, cancellationToken);
            progressListener?.OnStartupPhaseStep(2, 2);
            OnTwoScopeStartupCompleted(mainScope);
        }

        private async Task<LifetimeScope> CreateAndRunBaseScopeAsync(CancellationToken cancellationToken)
        {
            LifetimeScope baseScope = CreateChild(InstallBaseScopeInternal);
            EnsureScopeBuilt(baseScope);
            RegisterScopeInCrossLayer(baseScope.Container);
            IAsyncInitializationRunner baseRunner = baseScope.Container.Resolve<IAsyncInitializationRunner>();
            await baseRunner.RunAsync(baseScope.Container, cancellationToken);
            return baseScope;
        }

        private async Task<LifetimeScope> CreateAndRunMainScopeAsync(LifetimeScope baseScope, CancellationToken cancellationToken)
        {
            LifetimeScope mainScope = baseScope.CreateChild(InstallMainScopeInternal);
            EnsureScopeBuilt(mainScope);
            RegisterScopeInCrossLayer(mainScope.Container);
            IAsyncInitializationRunner mainRunner = mainScope.Container.Resolve<IAsyncInitializationRunner>();
            await mainRunner.RunAsync(mainScope.Container, cancellationToken);
            return mainScope;
        }

        private void EnsureScopeBuilt(LifetimeScope scope)
        {
            if (scope.Container == null)
            {
                scope.Build();
            }
        }

        private async Task RunLegacyIAsyncLayerInitializersAsync(IObjectResolver resolver, CancellationToken cancellationToken)
        {
            IAsyncLayerInitializable[]? pending = ResolveLegacyInitializersOrNull(resolver);
            if (pending == null || pending.Length == 0)
            {
                return;
            }

            Task[] tasks = new Task[pending.Length];
            for (int i = 0; i < pending.Length; i++)
            {
                tasks[i] = RunLegacyInitializerAsync(pending[i], resolver, cancellationToken);
            }

            await Task.WhenAll(tasks);
        }

        private IAsyncLayerInitializable[]? ResolveLegacyInitializersOrNull(IObjectResolver resolver)
        {
            IEnumerable<IAsyncLayerInitializable> resolved;
            try
            {
                resolved = resolver.Resolve<IEnumerable<IAsyncLayerInitializable>>();
            }
            catch (VContainerException)
            {
                return null;
            }

            IAsyncLayerInitializable[] pending = resolved.Where(i => i != null).ToArray();
            return pending.Length == 0 ? null : pending;
        }

        private async Task RunLegacyInitializerAsync(IAsyncLayerInitializable initializer, IObjectResolver resolver, CancellationToken cancellationToken)
        {
            try
            {
                await initializer.InitializeAsync(resolver, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Initialization failed in '{initializer.GetType().FullName}'.", exception);
            }
        }

        private void InstallBaseScopeInternal(IContainerBuilder builder)
        {
            builder.Register<AsyncInitializationRunner>(Lifetime.Singleton)
                .As<IAsyncInitializationRunner>();
            InstallBaseScope(builder);
        }

        private void InstallMainScopeInternal(IContainerBuilder builder)
        {
            builder.Register<AsyncInitializationRunner>(Lifetime.Singleton)
                .As<IAsyncInitializationRunner>();
            InstallMainScope(builder);
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

        private void RegisterScopeInCrossLayer(IObjectResolver resolver)
        {
            if (resolver == null)
            {
                return;
            }

            try
            {
                ICrossLayerObjectResolver crossLayerResolver = resolver.Resolve<ICrossLayerObjectResolver>();
                crossLayerResolver.RegisterScope(resolver);
            }
            catch (VContainerException)
            {
            }
        }

        protected abstract void InstallBaseScope(IContainerBuilder builder);

        protected abstract Task PrepareMainScopeAsync(IObjectResolver baseResolver, CancellationToken cancellationToken);

        protected abstract void InstallMainScope(IContainerBuilder builder);

        protected virtual void OnTwoScopeStartupCompleted(LifetimeScope mainScope)
        {
        }

        protected virtual IApplicationStartupProgress GetStartupProgressListener()
        {
            return StartupProgress;
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
