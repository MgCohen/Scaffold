using System;
using System.Collections.Generic;
using System.Linq;

namespace Scaffold.Scope
{
    /// <summary>
    /// Kahn-style topological levels for a directed graph (participant types only).
    /// </summary>
    public sealed class StartupOrderTopologicalLevels
    {
        public IReadOnlyList<IReadOnlyList<Type>> Compute(HashSet<Type> nodes, IReadOnlyList<(Type From, Type To)> edges)
        {
            ValidateArguments(nodes, edges);
            Dictionary<Type, int> inDegree = nodes.ToDictionary(t => t, _ => 0);
            Dictionary<Type, List<Type>> adjacency = nodes.ToDictionary(t => t, _ => new List<Type>());
            ApplyEdges(nodes, edges, adjacency, inDegree);
            return BuildLevels(adjacency, inDegree);
        }

        private void ValidateArguments(HashSet<Type> nodes, IReadOnlyList<(Type From, Type To)> edges)
        {
            if (nodes == null)
            {
                throw new ArgumentNullException(nameof(nodes));
            }

            if (edges == null)
            {
                throw new ArgumentNullException(nameof(edges));
            }
        }

        private void ApplyEdges(HashSet<Type> nodes, IReadOnlyList<(Type From, Type To)> edges, Dictionary<Type, List<Type>> adjacency, Dictionary<Type, int> inDegree)
        {
            foreach ((Type from, Type to) in edges)
            {
                if (!nodes.Contains(from) || !nodes.Contains(to))
                {
                    continue;
                }

                adjacency[from].Add(to);
                inDegree[to]++;
            }
        }

        private IReadOnlyList<IReadOnlyList<Type>> BuildLevels(Dictionary<Type, List<Type>> adjacency, Dictionary<Type, int> inDegree)
        {
            var levels = new List<IReadOnlyList<Type>>();
            List<Type> current = inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key).OrderBy(t => t.FullName).ToList();

            while (current.Count > 0)
            {
                levels.Add(current);
                current = DrainOneLevel(adjacency, inDegree, current);
            }

            if (inDegree.Values.Any(v => v > 0))
            {
                throw new InvalidOperationException("Startup order graph has a cycle among IStartupOrderParticipant types.");
            }

            return levels;
        }

        private List<Type> DrainOneLevel(Dictionary<Type, List<Type>> adjacency, Dictionary<Type, int> inDegree, IReadOnlyList<Type> current)
        {
            var next = new List<Type>();
            foreach (Type node in current)
            {
                foreach (Type successor in adjacency[node])
                {
                    inDegree[successor]--;
                    if (inDegree[successor] == 0)
                    {
                        next.Add(successor);
                    }
                }
            }

            return next.OrderBy(t => t.FullName).ToList();
        }
    }
}
