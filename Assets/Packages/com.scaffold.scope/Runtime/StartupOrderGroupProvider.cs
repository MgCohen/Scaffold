using System;
using System.Collections.Generic;
using System.Linq;
using Scaffold.Scope.Contracts;
using VContainer;

namespace Scaffold.Scope
{
    /// <summary>
    /// Builds ordered groups of <see cref="IStartupOrderParticipant"/> concrete types using inject-site edges and topological levels.
    /// </summary>
    public sealed class StartupOrderGroupProvider : IStartupOrderGroupProvider
    {
        public StartupOrderGroupProvider()
        {
            injectSiteAnalyzer = new StartupOrderInjectSiteAnalyzer();
            topologicalLevels = new StartupOrderTopologicalLevels();
        }

        private readonly StartupOrderInjectSiteAnalyzer injectSiteAnalyzer;
        private readonly StartupOrderTopologicalLevels topologicalLevels;

        public IReadOnlyList<IReadOnlyList<Type>> GetOrderedGroups(IObjectResolver resolver)
        {
            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }

            List<IStartupOrderParticipant> participants = resolver.Resolve<IEnumerable<IStartupOrderParticipant>>().ToList();
            return GetOrderedGroups(participants, resolver);
        }

        public IReadOnlyList<IReadOnlyList<Type>> GetOrderedGroups(IReadOnlyList<IStartupOrderParticipant> participants, IObjectResolver resolver)
        {
            ThrowIfParticipantsOrResolverNull(participants, resolver);
            HashSet<Type> membership = participants.Select(p => p.GetType()).ToHashSet();
            var edges = new List<(Type From, Type To)>();
            var edgeSeen = new HashSet<(Type From, Type To)>();
            foreach (Type type in membership)
            {
                CollectEdges(type, resolver, membership, edges, edgeSeen);
            }

            return topologicalLevels.Compute(membership, edges);
        }

        private void ThrowIfParticipantsOrResolverNull(IReadOnlyList<IStartupOrderParticipant> participants, IObjectResolver resolver)
        {
            if (participants == null)
            {
                throw new ArgumentNullException(nameof(participants));
            }

            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }
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
