using System.Linq;

namespace AAGen
{
    public static class SubgraphTopologyUtil
    {
        #region Static Methods
        /// <summary>
        /// Determines whether a sub-graph is shared across more than one source.
        /// </summary>
        /// <param name="subgraph">The sub-graph.</param>
        /// <returns>A value indicating whether a sub-graph is shared across more than one source.</returns>
        public static bool IsShared(this Subgraph subgraph)
        {
            return subgraph.Sources.Count > 1;
        }

        ///// <summary>
        ///// Determines if the sub-graph consists of a single node that has no connection to other nodes.
        ///// </summary>
        ///// <param name="subgraph">The sub-graph.</param>
        ///// <param name="dependencyGraph">The dependency graph.</param>
        ///// <returns>A value indicating whether the sub-graph consists of a single node that has no connection to other nodes.</returns>
        //public static bool IsSingleIsolatedNode(Subgraph subgraph, DependencyGraph dependencyGraph)
        //{
        //    // If there is at least one node, then:
        //    if (subgraph.Nodes.Count == 1)
        //    {
                // Retrieve a reference to the single node.
        //        var singleNode = subgraph.Nodes.ToList()[0];

        //        // If the node is a source node and a sink node, then it a single isolated node.
        //        // If it is a single isolated node, then:
        //        if (dependencyGraph.IsSourceNode(singleNode) && dependencyGraph.IsSinkNode(singleNode))
        //        {
        //            // The sub-graph consists of a single node that has no connection to other nodes.
        //            return true;
        //        }
        //    }

        //    // Otherwise there is no node or multiple, or one node that is not a single isolated node.

        //    // The sub-graph does not consist of a single node that has no connection to other nodes.
        //    return false;
        //}

        ///// <summary>
        ///// Determines if the sub-graph consists of a single node that is a source node of other subgraphs.
        ///// </summary>
        ///// <param name="subgraph">The sub-graph.</param>
        ///// <param name="dependencyGraph">The dependency graph.</param>
        ///// <returns>A value indicating whether the sub-graph consists of a single node that is a source node of other subgraphs.</returns>
        //public static bool IsSingleSourceNode(Subgraph subgraph, DependencyGraph dependencyGraph)
        //{
        //    // If there is at least one node, then:
        //    if (subgraph.Nodes.Count == 1)
        //    {
        //        // Retrieve a reference to the single node.
        //        var singleNode = subgraph.Nodes.ToList()[0];

        //        // If the node is a source node but not a sink node, then it a purely a source node.
        //        // If it is a source node, then:
        //        if (dependencyGraph.IsSourceNode(singleNode) && !dependencyGraph.IsSinkNode(singleNode))
        //        {
        //            // The sub-graph consists of a single node that is a source node of other sub-graphs.
        //            return true;
        //        }
        //    }

        //    // Otherwise there is no node or multiple, or one node that is not a source node.

        //    // The sub-graph does not consist of a single node that is a source node of other sub-graphs.
        //    return false;
        //}

        ///// <summary>
        ///// Determines if the sub-graph consists of a single node that is a sink node of other subgraphs.
        ///// </summary>
        ///// <param name="subgraph">The sub-graph.</param>
        ///// <param name="dependencyGraph">The dependency graph.</param>
        ///// <returns>A value indicating whether the sub-graph consists of a single node that is a sink node of other subgraphs.</returns>
        //public static bool IsSingleSinkNode(Subgraph subgraph, DependencyGraph dependencyGraph)
        //{
        //    // If there is at least one node, then:
        //    if (subgraph.Nodes.Count == 1)
        //    {
        //        // Retrieve a reference to the single node.
        //        var singleNode = subgraph.Nodes.ToList()[0];

        //        // If the node is not a source node but a sink node, then it is purely a sink node.
        //        // If it is a sink node, then:
        //        if (!dependencyGraph.IsSourceNode(singleNode) && dependencyGraph.IsSinkNode(singleNode))
        //        {
        //            return true;
        //        }
        //    }

        //    // Otherwise there is no node or multiple, or one node that is not a sink node.

        //    return false;
        //}

        /// <summary>
        /// Determines if the input subgraph consists of of multiple connected nodes including all the source nodes.
        /// </summary>
        /// <param name="subgraph">The subgraph.</param>
        /// <param name="dependencyGraph">The dependency graph.</param>
        /// <returns>A value indicating whether the input subgraph consists of a of multiple connected nodes including all the source nodes.</returns>
        public static bool IsHierarchy(Subgraph subgraph, DependencyGraph dependencyGraph)
        {
            // There is more than one node in the subgraph, and the sources in the subgraph are a subset of the nodes within the (dependency graph)?
            return subgraph.Nodes.Count > 1 && subgraph.Sources.IsSubsetOf(subgraph.Nodes);
        }
        #endregion
    }
}