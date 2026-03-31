using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.Scope.Contracts;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Scope
{
    public abstract class LayerInstallerBase
    {
        public IReadOnlyList<LayerInstallerBase> Children => children;

        private readonly List<LayerInstallerBase> children = new List<LayerInstallerBase>();
        private HashSet<IAsyncLayerInitializable> initializedRegistry = new HashSet<IAsyncLayerInitializable>(ReferenceComparer<IAsyncLayerInitializable>.Instance);
        private LayerInstallerBase parent;
        private LifetimeScope currentScope;
        private LifetimeScope finalScope;
        private LayerBuildProgressContext progressContext;

        public LayerInstallerBase AddChild(LayerInstallerBase child)
        {
            ValidateCanAddChild(child);
            child.parent = this;
            children.Add(child);
            return this;
        }

        private void ValidateCanAddChild(LayerInstallerBase child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            if (ReferenceEquals(child, this))
            {
                throw new InvalidOperationException("A layer cannot be added as its own child.");
            }

            if (child.parent != null)
            {
                throw new InvalidOperationException("A layer cannot have more than one parent.");
            }

            ValidateChildSubtreeRules(child);
        }

        private void ValidateChildSubtreeRules(LayerInstallerBase child)
        {
            if (ContainsInSubtree(child))
            {
                throw new InvalidOperationException("The child is already part of this tree.");
            }

            if (child.ContainsInSubtree(this))
            {
                throw new InvalidOperationException("Adding this child would create a cycle in the layer tree.");
            }
        }

        private bool ContainsInSubtree(LayerInstallerBase target)
        {
            if (ReferenceEquals(this, target))
            {
                return true;
            }

            for (int i = 0; i < children.Count; i++)
            {
                if (children[i].ContainsInSubtree(target))
                {
                    return true;
                }
            }

            return false;
        }

        public virtual void Reset()
        {
            progressContext = null;
            currentScope = null;
            finalScope = null;

            for (int i = 0; i < children.Count; i++)
            {
                children[i].Reset();
            }

            if (parent == null)
            {
                initializedRegistry.Clear();
            }
        }

        internal LifetimeScope GetFinalScope()
        {
            return finalScope;
        }

        protected Task BuildAsync(CancellationToken cancellationToken)
        {
            return ExecuteBuildPipelineAsync(cancellationToken);
        }

        public Task BuildAsRootAsync(LifetimeScope rootScope, CancellationToken cancellationToken, ILayeredScopeProgress progress = null)
        {
            if (rootScope == null)
            {
                throw new ArgumentNullException(nameof(rootScope));
            }

            ReferenceComparer<IAsyncLayerInitializable> comparer = ReferenceComparer<IAsyncLayerInitializable>.Instance;
            HashSet<IAsyncLayerInitializable> registry = new HashSet<IAsyncLayerInitializable>(comparer);
            AssignRegistry(registry);
            int totalLayers = CountLayerNodes();
            LayerBuildProgressContext layerContext = null;
            if (progress != null)
            {
                layerContext = new LayerBuildProgressContext(progress, totalLayers);
            }

            AssignProgressContext(layerContext);
            return BuildAsRootInternalAsync(rootScope, cancellationToken);
        }

        private void AssignRegistry(HashSet<IAsyncLayerInitializable> registry)
        {
            initializedRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
            for (int i = 0; i < children.Count; i++)
            {
                children[i].AssignRegistry(registry);
            }
        }

        private int CountLayerNodes()
        {
            int count = 1;
            for (int i = 0; i < children.Count; i++)
            {
                count += children[i].CountLayerNodes();
            }

            return count;
        }

        private void AssignProgressContext(LayerBuildProgressContext context)
        {
            progressContext = context;
            for (int i = 0; i < children.Count; i++)
            {
                children[i].AssignProgressContext(context);
            }
        }

        private async Task BuildAsRootInternalAsync(LifetimeScope rootScope, CancellationToken cancellationToken)
        {
            finalScope = await BuildFromParentAsync(rootScope, cancellationToken);
        }

        private async Task<LifetimeScope> BuildFromParentAsync(LifetimeScope parentScope, CancellationToken cancellationToken)
        {
            if (parentScope == null)
            {
                throw new ArgumentNullException(nameof(parentScope));
            }

            cancellationToken.ThrowIfCancellationRequested();
            currentScope = parentScope.CreateChild(builder => ConfigureChildScopeForParent(parentScope, builder));

            if (currentScope.Container == null)
            {
                currentScope.Build();
            }

            RegisterCurrentScopeInCrossLayerResolver();
            await ExecuteBuildPipelineAsync(cancellationToken);
            return finalScope ?? currentScope;
        }

        private void ConfigureChildScopeForParent(LifetimeScope parentScope, IContainerBuilder builder)
        {
            if (parent != null)
            {
                parent.ConfigureChildBuilder(this, parentScope.Container, builder);
            }

            Install(builder);
        }

        protected virtual void ConfigureChildBuilder(LayerInstallerBase child, IObjectResolver parentResolver, IContainerBuilder childBuilder)
        {
        }

        protected abstract void Install(IContainerBuilder builder);

        protected void Install(IContainerBuilder builder, IInstaller installer)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (installer == null)
            {
                throw new ArgumentNullException(nameof(installer));
            }

            installer.Install(builder);
        }

        private void RegisterCurrentScopeInCrossLayerResolver()
        {
            IObjectResolver resolver = currentScope?.Container;
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

        private async Task ExecuteBuildPipelineAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IObjectResolver resolver = currentScope?.Container;
            await InitializeAsync(resolver, cancellationToken);
            await OnCompletedAsync(resolver, cancellationToken);
            progressContext?.ReportCompletedStep();
            await BuildChildrenAsync(cancellationToken);
        }

        protected virtual Task InitializeAsync(IObjectResolver resolver, CancellationToken cancellationToken)
        {
            return InitializeResolvedInitializersAsync(resolver, cancellationToken);
        }

        private async Task InitializeResolvedInitializersAsync(IObjectResolver resolver, CancellationToken cancellationToken)
        {
            IReadOnlyList<IAsyncLayerInitializable> resolved = ResolveInitializers();
            IReadOnlyList<IAsyncLayerInitializable> pending = FilterPendingInitializers(resolved);
            if (pending.Count == 0)
            {
                return;
            }

            await RunAllInitializersAsync(pending, resolver, cancellationToken);
            RegisterPendingAsInitialized(pending);
        }

        private async Task RunAllInitializersAsync(IReadOnlyList<IAsyncLayerInitializable> pending, IObjectResolver resolver, CancellationToken cancellationToken)
        {
            Task[] tasks = new Task[pending.Count];
            for (int i = 0; i < pending.Count; i++)
            {
                tasks[i] = RunInitializerAsync(pending[i], resolver, cancellationToken);
            }

            await Task.WhenAll(tasks);
        }

        private void RegisterPendingAsInitialized(IReadOnlyList<IAsyncLayerInitializable> pending)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                initializedRegistry.Add(pending[i]);
            }
        }

        protected virtual IReadOnlyList<IAsyncLayerInitializable> ResolveInitializers()
        {
            if (currentScope == null)
            {
                return Array.Empty<IAsyncLayerInitializable>();
            }

            IEnumerable<IAsyncLayerInitializable> resolved = TryResolveInitializersEnumerable();
            if (resolved == null)
            {
                return Array.Empty<IAsyncLayerInitializable>();
            }

            return resolved.Where(initializer => initializer != null).ToArray();
        }

        private IEnumerable<IAsyncLayerInitializable> TryResolveInitializersEnumerable()
        {
            try
            {
                return currentScope.Container.Resolve<IEnumerable<IAsyncLayerInitializable>>();
            }
            catch (VContainerException)
            {
                return null;
            }
        }

        protected virtual IReadOnlyList<IAsyncLayerInitializable> FilterPendingInitializers(IReadOnlyList<IAsyncLayerInitializable> resolved)
        {
            if (resolved == null || resolved.Count == 0)
            {
                return Array.Empty<IAsyncLayerInitializable>();
            }

            List<IAsyncLayerInitializable> pending = new List<IAsyncLayerInitializable>();
            for (int i = 0; i < resolved.Count; i++)
            {
                IAsyncLayerInitializable initializer = resolved[i];
                if (initializer == null || initializedRegistry.Contains(initializer))
                {
                    continue;
                }

                pending.Add(initializer);
            }

            return pending;
        }

        private async Task RunInitializerAsync(IAsyncLayerInitializable initializer, IObjectResolver resolver, CancellationToken cancellationToken)
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

        protected virtual Task OnCompletedAsync(IObjectResolver resolver, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        protected virtual async Task BuildChildrenAsync(CancellationToken cancellationToken)
        {
            LifetimeScope last = currentScope;
            for (int i = 0; i < children.Count; i++)
            {
                LayerInstallerBase child = children[i];
                last = await BuildChildFromParentAsync(child, currentScope, cancellationToken);
            }

            finalScope = last;
        }

        private static async Task<LifetimeScope> BuildChildFromParentAsync(LayerInstallerBase child, LifetimeScope parentScope, CancellationToken cancellationToken)
        {
            return await child.BuildFromParentAsync(parentScope, cancellationToken);
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();

            public bool Equals(T x, T y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return obj == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
