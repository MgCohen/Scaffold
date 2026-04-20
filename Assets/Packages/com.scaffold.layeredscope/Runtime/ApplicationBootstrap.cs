using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.LayeredScope.Internal;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scaffold.LayeredScope
{
    public abstract class ApplicationBootstrap : LifetimeScope
    {
        protected ApplicationHost Host { get; private set; }

        public Task ReadyTask => readyTcs.Task;

        private readonly TaskCompletionSource<bool> readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected virtual async void Start()
        {
            try
            {
                await RunStartupAsync();
            }
            catch (OperationCanceledException oce)
            {
                readyTcs.TrySetCanceled(oce.CancellationToken);
            }
            catch (Exception ex)
            {
                LogStartupFailed(ex);
                await SafeOnStartupFailedAsync(ex);
                readyTcs.TrySetException(ex);
            }
        }

        private async Task SafeOnStartupFailedAsync(Exception ex)
        {
            try
            {
                await OnStartupFailedAsync(ex, destroyCancellationToken);
            }
            catch (Exception cbEx)
            {
                Debug.LogError($"[ApplicationBootstrap] OnStartupFailedAsync threw: {cbEx.Message}\n{cbEx.StackTrace}");
            }
        }

        private async Task RunStartupAsync()
        {
            CancellationToken ct = destroyCancellationToken;
            Host = new ApplicationHost(this, CreateScheduler());
            await Host.InstallAllAsync(GetInitialLayers(), ct);
            await OnReadyAsync(ct);
            readyTcs.TrySetResult(true);
        }

        protected virtual IInLayerScheduler CreateScheduler()
        {
            return new ParallelScheduler();
        }

        protected abstract IEnumerable<IScopeLayer> GetInitialLayers();

        protected virtual Task OnReadyAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        protected virtual Task OnStartupFailedAsync(Exception ex, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        private void LogStartupFailed(Exception ex)
        {
            Debug.LogError($"[ApplicationBootstrap] Startup failed: {ex.Message}\n{ex.StackTrace}");
        }

        protected sealed override void Configure(IContainerBuilder builder)
        {
            var proxy = new LayerResolverProxy();
            builder.RegisterInstance<LayerResolverProxy, ILayerResolver>(proxy);
            ConfigureApplication(builder);
        }

        protected virtual void ConfigureApplication(IContainerBuilder builder)
        {

        }
    }
}
