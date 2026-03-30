using System.Collections.Generic;

namespace AAGen
{
    /// <summary>
    /// Represents a grapg that is a sub-set of a larger <see cref="DependencyGraph"/> instance.
    /// </summary>
    public class Subgraph
    {
        // NOTE: Investigate the pros & cons of using hashsets.

        #region Fields
        /// <summary>
        /// The set of nodes from the <see cref="DependencyGraph"/> that defines this sub-graph. 
        /// </summary>
        public HashSet<AssetNode> Nodes = new HashSet<AssetNode>();

        /// <summary>
        /// The set of nodes from the <see cref="DependencyGraph"/> that defines the source nodes in this sub-graph.
        /// </summary>
        public HashSet<AssetNode> Sources = new HashSet<AssetNode>();
        
        /// <summary>
        /// A hash formed from the unique properties of all <see cref="Sources"/> in this sub-graph.
        /// </summary>
        public int HashOfSources;
        #endregion
    }
}