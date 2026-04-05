using System;
using System.Collections.Generic;
using VContainer;

namespace Scaffold.Scope.InitializationGraph
{
    /// <summary>
    /// Builds edges from VContainer inject sites among <see cref="IAsyncInitializable"/> participants and computes topological waves.
    /// </summary>
    internal sealed class InitializationGraphBuilder
    {
        public InitializationGraphBuilder()
        {
            injectSiteAnalyzer = new InjectSiteAnalyzer();
            topologicalLevels = new TopologicalLevels();
        }

        private readonly InjectSiteAnalyzer injectSiteAnalyzer;
        private readonly TopologicalLevels topologicalLevels;

        public IReadOnlyList<IReadOnlyList<Type>> ComputeTopologicalLevels(HashSet<Type> membership, IObjectResolver resolver)
        {
            if (membership == null)
            {
                throw new ArgumentNullException(nameof(membership));
            }

            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }

            var edges = new List<(Type From, Type To)>();
            var edgeSeen = new HashSet<(Type From, Type To)>();
            foreach (Type type in membership)
            {
                CollectEdges(type, resolver, membership, edges, edgeSeen);
            }

            return topologicalLevels.Compute(membership, edges);
        }

        private void CollectEdges(Type implementationType, IObjectResolver resolver, HashSet<Type> membership, List<(Type From, Type To)> edges, HashSet<(Type From, Type To)> edgeSeen)
        {
            Type to = implementationType;
            foreach ((Type dependencyType, object key) in injectSiteAnalyzer.GetDependencySites(implementationType))
            {
                TryAddEdgeForDependency(dependencyType, key, resolver, membership, to, edges, edgeSeen);
            }
        }

        private void TryAddEdgeForDependency(Type dependencyType, object key, IObjectResolver resolver, HashSet<Type> membership, Type to, List<(Type From, Type To)> edges, HashSet<(Type From, Type To)> edgeSeen)
        {
            if (!resolver.TryGetRegistration(dependencyType, out Registration registration, key))
            {
                return;
            }

            Type from = registration.ImplementationType;
            if (!membership.Contains(from) || !membership.Contains(to))
            {
                return;
            }

            var pair = (from, to);
            if (edgeSeen.Add(pair))
            {
                edges.Add(pair);
            }
        }
    }
}
