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
    public sealed class AppFlowHost : ILayerResolver
    {
        public AppFlowHost(LifetimeScope root, IInLayerScheduler scheduler = null, IAppFlowErrorHandler errorHandler = null, AppFlowProgress progress = null)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            this.scheduler = scheduler ?? new ParallelScheduler();
            this.errorHandler = errorHandler;
            this.progress = progress;
            proxy = root.Container.Resolve<LayerResolverProxy>();
            stack.Push(LayerEntry.CreateRoot(root));
            proxy.Bind(root.Container);
        }

        public IObjectResolver Top => stack.Peek().Scope.Container;

        private readonly Stack<LayerEntry> stack = new();
        private readonly HashSet<IAsyncInitializable> seenInitializables = new();
        private readonly HashSet<IAsyncDisposable> seenDisposables = new();
        private readonly LayerResolverProxy proxy;
        private readonly IInLayerScheduler scheduler;
        private readonly IAppFlowErrorHandler errorHandler;
        private readonly AppFlowProgress progress;

        private int sessionDepth;

        private Action<float> activeProgressHandler;

        private ILayerProgressSource activeProgressSource;

        public bool TryResolve<T>(out T value)
        {
            return Top.TryResolve(out value);
        }

        public T Resolve<T>()
        {
            return Top.Resolve<T>();
        }

        public async Task InstallAllAsync(IEnumerable<IScopeLayer> layers, CancellationToken ct)
        {
            if (layers == null)
            {
                throw new ArgumentNullException(nameof(layers));
            }

            int pushedHere = 0;
            try
            {
                pushedHere = await PushEachLayerAsync(layers, ct);
            }
            catch
            {
                await UnwindAsync(pushedHere);
                throw;
            }
        }

        private async Task<int> PushEachLayerAsync(IEnumerable<IScopeLayer> layers, CancellationToken ct)
        {
            int count = 0;
            foreach (var layer in layers)
            {
                await PushAsync(layer, ct);
                count++;
            }

            return count;
        }

        private async Task UnwindAsync(int count)
        {
            for (int i = 0; i < count; i++)
            {
                IScopeLayer top = stack.Count > 0 ? stack.Peek().Layer : null;
                try
                {
                    await PopAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    ReportLayerError(LayerOperation.Unwind, top, ex);
                }
            }
        }

        public async Task PushAsync(IScopeLayer layer, CancellationToken ct)
        {
            if (layer == null)
            {
                throw new ArgumentNullException(nameof(layer));
            }

            bool adHocSession = BeginAdHocPushIfNeeded(layer);
            int layerIndex = progress != null ? progress.HostAddLayer(layer.Name) : -1;
            try
            {
                await RunPushWithSessionAsync(layer, ct, layerIndex, adHocSession);
            }
            catch (Exception ex)
            {
                HandlePushFailed(layerIndex, adHocSession, ex);
                throw;
            }
        }

        private bool BeginAdHocPushIfNeeded(IScopeLayer layer)
        {
            if (sessionDepth != 0)
            {
                return false;
            }

            BeginSession($"Push:{layer.Name}", 1);
            return true;
        }

        private async Task RunPushWithSessionAsync(IScopeLayer layer, CancellationToken ct, int layerIndex, bool adHocSession)
        {
            await ExecutePushCoreAsync(layer, ct, layerIndex);
            if (adHocSession)
            {
                EndSession(null);
            }
        }

        public async Task PopAsync(CancellationToken ct)
        {
            LayerEntry entry = RemoveTopEntry();
            bool adHocSession = sessionDepth == 0;
            int layerIndex = BeginPopSessionIfNeeded(entry, adHocSession);
            await RunPopDisposeAsync(entry, layerIndex, adHocSession);
            CompletePopProgress(layerIndex, adHocSession);
            _ = ct;
        }

        private async Task RunPopDisposeAsync(LayerEntry entry, int layerIndex, bool adHocSession)
        {
            try
            {
                await RunDisposeWaveAsync(entry);
            }
            catch (Exception ex)
            {
                HandlePopDisposeFailed(layerIndex, adHocSession, ex);
                throw;
            }
            finally
            {
                DisposePopScopeResources(entry);
            }
        }

        private async Task ExecutePushCoreAsync(IScopeLayer layer, CancellationToken ct, int layerIndex)
        {
            if (layer is IAsyncScopeLayer)
            {
                SetLayerProgressStatus(layerIndex, LayerStatus.Preparing);
                await PrepareLayerAsync(layer, ct);
            }

            SetLayerProgressStatus(layerIndex, LayerStatus.Installing);
            var publisher = new LayerPublisher();
            LifetimeScope child = BuildChildScope(layer, publisher);
            await AttemptPushAsync(layer, child, publisher, ct, layerIndex);
        }

        private void HandlePushFailed(int layerIndex, bool adHocSession, Exception ex)
        {
            SetLayerProgressStatus(layerIndex, LayerStatus.Failed);
            if (adHocSession)
            {
                EndSession(ex);
            }
        }

        private int BeginPopSessionIfNeeded(LayerEntry entry, bool adHocSession)
        {
            if (!adHocSession || entry.Layer == null)
            {
                return -1;
            }

            BeginSession($"Pop:{entry.Layer.Name}", 1);
            int idx = progress != null ? progress.HostAddLayer(entry.Layer.Name) : -1;
            SetLayerProgressStatus(idx, LayerStatus.Disposing);
            return idx;
        }

        private void HandlePopDisposeFailed(int layerIndex, bool adHocSession, Exception ex)
        {
            SetLayerProgressStatus(layerIndex, LayerStatus.Failed);
            if (adHocSession)
            {
                EndSession(ex);
            }
        }

        private void CompletePopProgress(int layerIndex, bool adHocSession)
        {
            SetLayerProgressStatus(layerIndex, LayerStatus.Disposed);
            if (adHocSession)
            {
                EndSession(null);
            }
        }

        private void DisposePopScopeResources(LayerEntry entry)
        {
            ReleaseEntryMembership(entry);
            entry.Scope.Dispose();
            RebindProxyToTop();
        }

        private async Task PrepareLayerAsync(IScopeLayer layer, CancellationToken ct)
        {
            if (layer is not IAsyncScopeLayer asyncLayer)
            {
                return;
            }

            try
            {
                await asyncLayer.PrepareAsync(Top, ct);
            }
            catch (Exception ex)
            {
                ReportLayerError(LayerOperation.Prepare, layer, ex);
                throw;
            }
        }

        private LifetimeScope BuildChildScope(IScopeLayer layer, LayerPublisher publisher)
        {
            LifetimeScope child = stack.Peek().Scope.CreateChild(b => ConfigureChildBuilder(layer, publisher, b));
            child.name = layer.Name;
            EnsureChildScopeContainerBuilt(child);
            return child;
        }

        private void ConfigureChildBuilder(IScopeLayer layer, LayerPublisher publisher, IContainerBuilder builder)
        {
            foreach (LayerEntry e in EnumerateStackRootToParent())
            {
                e.Publisher.Apply(builder);
            }

            builder.RegisterInstance<ILayerPublisher>(publisher);
            layer.Install(builder);
        }

        private IEnumerable<LayerEntry> EnumerateStackRootToParent()
        {
            LayerEntry[] arr = stack.ToArray();
            for (int i = arr.Length - 1; i >= 0; i--)
            {
                yield return arr[i];
            }
        }

        private void EnsureChildScopeContainerBuilt(LifetimeScope child)
        {
            if (child.Container != null)
            {
                return;
            }

            child.Build();
        }

        private async Task AttemptPushAsync(IScopeLayer layer, LifetimeScope child, LayerPublisher publisher, CancellationToken ct, int layerIndex)
        {
            IAsyncInitializable[] inits = Array.Empty<IAsyncInitializable>();
            IAsyncDisposable[] disposables = Array.Empty<IAsyncDisposable>();
            try
            {
                (inits, disposables) = await RunLayerInitAndCollectDisposablesAsync(layer, child, ct, layerIndex);
                FinishSuccessfulPush(layer, child, publisher, inits, disposables, layerIndex);
            }
            catch (Exception ex)
            {
                FailPush(layer, child, inits, disposables, ex);
                throw;
            }
        }

        private async Task<(IAsyncInitializable[] inits, IAsyncDisposable[] disposables)> RunLayerInitAndCollectDisposablesAsync(IScopeLayer layer, LifetimeScope child, CancellationToken ct, int layerIndex)
        {
            BindLayerProgressIfNeeded(layer, layerIndex);
            IAsyncInitializable[] inits = CollectFresh<IAsyncInitializable>(child, seenInitializables);
            var runner = new LayerInitRunner(child.Container, inits, scheduler);

            if (layer is IInitializableLayer custom)
            {
                await InitializeCustomLayerAsync(custom, runner, inits, layer, ct);
            }
            else
            {
                await runner.RunDefaultInitAsync(ct);
            }

            return (inits, CollectFresh<IAsyncDisposable>(child, seenDisposables));
        }

        private async Task InitializeCustomLayerAsync(IInitializableLayer custom, LayerInitRunner runner, IAsyncInitializable[] inits, IScopeLayer layer, CancellationToken ct)
        {
            await custom.InitializeAsync(runner, ct);
            if (!runner.DefaultInitInvoked && inits.Length > 0)
            {
                Debug.LogWarning(
                    $"[AppFlow] '{layer.Name}' overrode InitializeAsync but did not call RunDefaultInitAsync; "
                    + $"{inits.Length} IAsyncInitializable instance(s) were skipped.");
            }
        }

        private void BindLayerProgressIfNeeded(IScopeLayer layer, int layerIndex)
        {
            SetLayerProgressStatus(layerIndex, LayerStatus.Initializing);
            if (layer is ILayerProgressSource src && progress != null && layerIndex >= 0)
            {
                activeProgressSource = src;
                activeProgressHandler = v => progress.HostSetSubProgress(layerIndex, v);
                src.ProgressChanged += activeProgressHandler;
                progress.HostSetSubProgress(layerIndex, src.Progress);
            }
        }

        private void FinishSuccessfulPush(IScopeLayer layer, LifetimeScope child, LayerPublisher publisher, IAsyncInitializable[] inits, IAsyncDisposable[] disposables, int layerIndex)
        {
            RecordEntry(new LayerEntry(layer, child, inits, disposables, publisher));
            SetLayerProgressStatus(layerIndex, LayerStatus.Ready);
            UnbindLayerProgress();
            Debug.Log($"[AppFlow] Pushed '{layer.Name}' (init: {inits.Length}, dispose: {disposables.Length}).");
        }

        private void SetLayerProgressStatus(int layerIndex, LayerStatus status)
        {
            if (layerIndex >= 0)
            {
                progress?.HostSetLayerStatus(layerIndex, status);
            }
        }

        private LayerEntry RemoveTopEntry()
        {
            if (stack.Count <= 1)
            {
                throw new InvalidOperationException("[AppFlow] Cannot pop the root scope.");
            }

            return stack.Pop();
        }

        private async Task RunDisposeWaveAsync(LayerEntry entry)
        {
            if (entry.OwnedDisposables.Length == 0)
            {
                return;
            }

            try
            {
                var tasks = entry.OwnedDisposables.Select(d => d.DisposeAsync().AsTask());
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                ReportLayerError(LayerOperation.Dispose, entry.Layer, ex);
                throw;
            }
        }

        private void ReleaseEntryMembership(LayerEntry entry)
        {
            ClearSeenMembers(entry.OwnedInitializables, entry.OwnedDisposables);
        }

        private void RebindProxyToTop()
        {
            proxy.Bind(stack.Peek().Scope.Container);
        }

        private T[] CollectFresh<T>(LifetimeScope child, HashSet<T> seen) where T : class
        {
            if (child == null || child.Container == null)
            {
                return Array.Empty<T>();
            }

            if (!child.Container.TryResolve(out IReadOnlyList<T> all))
            {
                return Array.Empty<T>();
            }

            return all.Where(seen.Add).ToArray();
        }

        private void FailPush(IScopeLayer layer, LifetimeScope child, IAsyncInitializable[] inits, IAsyncDisposable[] disposables, Exception ex)
        {
            UnbindLayerProgress();
            ReportLayerError(LayerOperation.Init, layer, ex);
            ClearSeenMembers(inits, disposables);
            child.Dispose();
        }

        private void UnbindLayerProgress()
        {
            if (activeProgressSource != null && activeProgressHandler != null)
            {
                activeProgressSource.ProgressChanged -= activeProgressHandler;
            }

            activeProgressSource = null;
            activeProgressHandler = null;
        }

        private void ReportLayerError(LayerOperation op, IScopeLayer layer, Exception ex)
        {
            if (errorHandler == null)
            {
                return;
            }

            AppFlowErrorPhase phase = CreateErrorPhaseFromLayerOperation(op);
            errorHandler.Report(new AppFlowErrorInfo(phase, layer?.Name, "AppFlowHost", ex, DateTime.UtcNow));
        }

        private AppFlowErrorPhase CreateErrorPhaseFromLayerOperation(LayerOperation op)
        {
            return op switch
            {
                LayerOperation.Prepare => AppFlowErrorPhase.Prepare,
                LayerOperation.Init => AppFlowErrorPhase.Init,
                LayerOperation.Dispose => AppFlowErrorPhase.Dispose,
                LayerOperation.Unwind => AppFlowErrorPhase.Unwind,
                _ => AppFlowErrorPhase.Manual
            };
        }

        private void ClearSeenMembers(IAsyncInitializable[] inits, IAsyncDisposable[] disposables)
        {
            foreach (var i in inits)
            {
                seenInitializables.Remove(i);
            }

            foreach (var d in disposables)
            {
                seenDisposables.Remove(d);
            }
        }

        private void RecordEntry(LayerEntry entry)
        {
            stack.Push(entry);
            proxy.Bind(entry.Scope.Container);
        }

        public void BeginSession(string name, int expectedLayers)
        {
            if (sessionDepth > 0)
            {
                throw new InvalidOperationException("[AppFlow] Nested BeginSession is not supported.");
            }

            sessionDepth++;
            progress?.HostBeginSession(name, expectedLayers);
        }

        public void EndSession(Exception fault)
        {
            if (sessionDepth == 0)
            {
                return;
            }

            sessionDepth--;
            AppFlowOutcome outcome = CreateOutcomeFromFault(fault);
            progress?.HostEndSession(outcome);
        }

        private AppFlowOutcome CreateOutcomeFromFault(Exception fault)
        {
            if (fault == null)
            {
                return AppFlowOutcome.CreateSuccess();
            }

            if (fault is OperationCanceledException)
            {
                return AppFlowOutcome.CreateCancelled();
            }

            return AppFlowOutcome.CreateFailed(fault);
        }
    }
}
