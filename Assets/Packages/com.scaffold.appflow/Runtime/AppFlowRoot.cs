using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.AppFlow.Internal;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scaffold.AppFlow
{
    public abstract class AppFlowRoot : LifetimeScope
    {
        protected AppFlowHost Host { get; private set; }

        public Task ReadyTask => readyTcs.Task;

        public IAppFlowProgress Progress => progress;

        public IAppFlowErrorHandler Errors => errorHandler;

        private readonly TaskCompletionSource<bool> readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private AppFlowProgress progress;

        private AppFlowErrorHandler errorHandler;

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
                await ReportStartupFailureAndCompleteReadyAsync(ex);
            }
        }

        private async Task ReportStartupFailureAndCompleteReadyAsync(Exception ex)
        {
            TryReportStartupPhaseToHandler(ex);
            await RunStartupFailedCallbackSafeAsync(ex);
            readyTcs.TrySetException(ex);
        }

        private void TryReportStartupPhaseToHandler(Exception ex)
        {
            try
            {
                errorHandler?.Report(new AppFlowErrorInfo(AppFlowErrorPhase.Startup, null, "AppFlowRoot", ex, DateTime.UtcNow));
            }
            catch (Exception reportEx)
            {
                Debug.LogError($"[AppFlow] Failed to report startup failure: {reportEx.Message}\n{reportEx.StackTrace}");
            }
        }

        private async Task RunStartupFailedCallbackSafeAsync(Exception ex)
        {
            try
            {
                await SafeOnStartupFailedAsync(ex);
            }
            catch (Exception callbackEx)
            {
                Debug.LogError($"[AppFlow] OnStartupFailedAsync chain failed: {callbackEx.Message}\n{callbackEx.StackTrace}");
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
                errorHandler?.Report(new AppFlowErrorInfo(AppFlowErrorPhase.Manual, null, "AppFlowRoot.OnStartupFailedAsync", cbEx, DateTime.UtcNow));
            }
        }

        private async Task RunStartupAsync()
        {
            CancellationToken ct = destroyCancellationToken;
            IReadOnlyList<IScopeLayer> layers = PrepareHostAndLayers();
            await ExecuteStartupSessionAsync(ct, layers);
        }

        private IReadOnlyList<IScopeLayer> PrepareHostAndLayers()
        {
            Host = new AppFlowHost(this, CreateScheduler(), errorHandler, progress);
            return GetInitialLayers().ToList();
        }

        private async Task ExecuteStartupSessionAsync(CancellationToken ct, IReadOnlyList<IScopeLayer> layers)
        {
            Exception fault = null;
            Host.BeginSession("Startup", layers.Count);
            try
            {
                await RunStartupBodyAsync(ct, layers);
            }
            catch (Exception ex)
            {
                fault = ex;
                throw;
            }
            finally
            {
                Host.EndSession(fault);
            }
        }

        private async Task RunStartupBodyAsync(CancellationToken ct, IReadOnlyList<IScopeLayer> layers)
        {
            await Host.InstallAllAsync(layers, ct);
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

        protected sealed override void Configure(IContainerBuilder builder)
        {
            var proxy = new LayerResolverProxy();
            builder.RegisterInstance<LayerResolverProxy, ILayerResolver>(proxy);

            errorHandler = new AppFlowErrorHandler();
            progress = new AppFlowProgress(errorHandler);
            builder.RegisterInstance(errorHandler).As<IAppFlowErrorHandler>().AsSelf();
            builder.RegisterInstance(progress).As<IAppFlowProgress>().AsSelf();

            ConfigureApplication(builder);
        }

        protected virtual void ConfigureApplication(IContainerBuilder builder)
        {
        }
    }
}
