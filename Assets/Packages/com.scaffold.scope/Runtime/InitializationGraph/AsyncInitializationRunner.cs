using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.Scope.Contracts;
using Scaffold.Scope.InitializationGraph;
using VContainer;

namespace Scaffold.Scope
{
    public sealed class AsyncInitializationRunner : IAsyncInitializationRunner
    {
        public AsyncInitializationRunner()
        {
            graphBuilder = new InitializationGraphBuilder();
        }

        private readonly InitializationGraphBuilder graphBuilder;

        public async Task RunAsync(IObjectResolver resolver, CancellationToken cancellationToken)
        {
            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }

            List<IAsyncInitializable> participants = resolver.Resolve<IEnumerable<IAsyncInitializable>>().ToList();
            if (participants.Count == 0)
            {
                return;
            }

            ILookup<Type, IAsyncInitializable> byConcrete = participants.ToLookup(p => p.GetType());
            HashSet<Type> membership = byConcrete.Select(g => g.Key).ToHashSet();
            IReadOnlyList<IReadOnlyList<Type>> levels = graphBuilder.ComputeTopologicalLevels(membership, resolver);
            await RunLevelsAsync(byConcrete, levels, cancellationToken);
        }

        private async Task RunLevelsAsync(ILookup<Type, IAsyncInitializable> byConcrete, IReadOnlyList<IReadOnlyList<Type>> levels, CancellationToken cancellationToken)
        {
            foreach (IReadOnlyList<Type> level in levels)
            {
                await RunOneLevelAsync(byConcrete, level, cancellationToken);
            }
        }

        private async Task RunOneLevelAsync(ILookup<Type, IAsyncInitializable> byConcrete, IReadOnlyList<Type> level, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            foreach (Type implType in level)
            {
                foreach (IAsyncInitializable instance in byConcrete[implType])
                {
                    tasks.Add(instance.InitializeAsync(cancellationToken));
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }
        }
    }
}
