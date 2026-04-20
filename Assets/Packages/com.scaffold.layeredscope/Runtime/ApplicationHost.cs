using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.LayeredScope.Internal;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scaffold.LayeredScope
{
    public sealed class ApplicationHost : ILayerResolver
    {
        public ApplicationHost(LifetimeScope root, IInLayerScheduler scheduler = null)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            this.scheduler = scheduler ?? new ParallelScheduler();
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

        public event Action<LayerOperation, IScopeLayer, Exception> LayerFailed;

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
                    Debug.LogError($"[LayeredScope] Unwind failure: {ex.Message}");
                    RaiseLayerFailed(LayerOperation.Unwind, top, ex);
                }
            }
        }

        public async Task PushAsync(IScopeLayer layer, CancellationToken ct)
        {
            if (layer == null)
            {
                throw new ArgumentNullException(nameof(layer));
            }

            await PrepareLayerAsync(layer, ct);
            var publisher = new LayerPublisher();
            LifetimeScope child = BuildChildScope(layer, publisher);
            await AttemptPushAsync(layer, child, publisher, ct);
        }

        public async Task PopAsync(CancellationToken ct)
        {
            LayerEntry entry = RemoveTopEntry();
            try
            {
                await RunDisposeWaveAsync(entry);
            }
            finally
            {
                ReleaseMembership(entry);
                entry.Scope.Dispose();
                RebindProxyToTop();
            }

            _ = ct;
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
                Debug.LogError($"[LayeredScope] PrepareAsync failed for '{layer.Name}': {ex.Message}\n{ex.StackTrace}");
                RaiseLayerFailed(LayerOperation.Prepare, layer, ex);
                throw;
            }
        }

        private LifetimeScope BuildChildScope(IScopeLayer layer, LayerPublisher publisher)
        {
            LifetimeScope child = stack.Peek().Scope.CreateChild(b => ConfigureChildBuilder(layer, publisher, b));
            child.name = layer.Name;
            return child;
        }

        private void ConfigureChildBuilder(IScopeLayer layer, LayerPublisher publisher, IContainerBuilder builder)
        {
            foreach (LayerEntry entry in EnumerateStackRootToParent())
            {
                entry.Publisher.Apply(builder);
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

        private async Task AttemptPushAsync(IScopeLayer layer, LifetimeScope child, LayerPublisher publisher, CancellationToken ct)
        {
            IAsyncInitializable[] inits = Array.Empty<IAsyncInitializable>();
            IAsyncDisposable[] disposables = Array.Empty<IAsyncDisposable>();
            try
            {
                inits = CollectFresh<IAsyncInitializable>(child, seenInitializables);
                await RunInitWaveAsync(inits, ct);
                disposables = CollectFresh<IAsyncDisposable>(child, seenDisposables);
                FinishSuccessfulPush(layer, child, publisher, inits, disposables);
            }
            catch (Exception ex)
            {
                FailPush(layer, child, inits, disposables, ex);
                throw;
            }
        }

        private LayerEntry RemoveTopEntry()
        {
            if (stack.Count <= 1)
            {
                throw new InvalidOperationException("[LayeredScope] Cannot pop the root scope.");
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
                Debug.LogError($"[LayeredScope] DisposeAsync failed for '{entry.Layer?.Name}': {ex.Message}\n{ex.StackTrace}");
                RaiseLayerFailed(LayerOperation.Dispose, entry.Layer, ex);
                throw;
            }
        }

        private void ReleaseMembership(LayerEntry entry)
        {
            ReleaseMembership(entry.OwnedInitializables, entry.OwnedDisposables);
        }

        private void RebindProxyToTop()
        {
            proxy.Bind(stack.Peek().Scope.Container);
        }

        private T[] CollectFresh<T>(LifetimeScope child, HashSet<T> seen) where T : class
        {
            if (!child.Container.TryResolve(out IReadOnlyList<T> all))
            {
                return Array.Empty<T>();
            }

            return all.Where(seen.Add).ToArray();
        }

        private Task RunInitWaveAsync(IAsyncInitializable[] inits, CancellationToken ct)
        {
            return inits.Length == 0 ? Task.CompletedTask : scheduler.RunAsync(inits, ct);
        }

        private void FinishSuccessfulPush(IScopeLayer layer, LifetimeScope child, LayerPublisher publisher, IAsyncInitializable[] inits, IAsyncDisposable[] disposables)
        {
            RecordEntry(new LayerEntry(layer, child, inits, disposables, publisher));
            Debug.Log($"[LayeredScope] Pushed '{layer.Name}' (init: {inits.Length}, dispose: {disposables.Length}).");
        }

        private void FailPush(IScopeLayer layer, LifetimeScope child, IAsyncInitializable[] inits, IAsyncDisposable[] disposables, Exception ex)
        {
            Debug.LogError($"[LayeredScope] Push failed for '{layer.Name}': {ex.Message}\n{ex.StackTrace}");
            ReleaseMembership(inits, disposables);
            child.Dispose();
            RaiseLayerFailed(LayerOperation.Init, layer, ex);
        }

        private void RaiseLayerFailed(LayerOperation op, IScopeLayer layer, Exception ex)
        {
            var handler = LayerFailed;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(op, layer, ex);
            }
            catch (Exception cbEx)
            {
                Debug.LogError($"[LayeredScope] LayerFailed handler threw: {cbEx.Message}\n{cbEx.StackTrace}");
            }
        }

        private void ReleaseMembership(IAsyncInitializable[] inits, IAsyncDisposable[] disposables)
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
    }
}
