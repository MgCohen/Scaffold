using System.Collections.Generic;

namespace AAGen
{
    /// <summary>
    /// Represents a set of data shared across all of AAGen. 
    /// </summary>
    public class DataContainer
    {
        #region Fields
        /// <summary>
        /// The unique set of asset file paths that are included as inputs to the AAGen pipeline to build the dependency graph.
        /// </summary>
        public HashSet<string> InputAssets;

        /// <summary>
        /// A reference to the dependency graph.
        /// </summary>
        public DependencyGraph DependencyGraph;

        /// <summary>
        /// The file path of the AAGen settings asset.
        /// </summary>
        public string SettingsFilePath;

        /// <summary>
        /// A reference to the loaded instance of the AAGen settings asset.
        /// </summary>
        public AagenSettings Settings;

        /// <summary>
        /// A set of nodes that represent assets ignored by the input rules.
        /// </summary>
        public HashSet<AssetNode> ExcludedAssets;

        /// <summary>
        /// A collection of sub-graphs associated by a unique set of source nodes.
        /// </summary>
        public Dictionary<int, Subgraph> Subgraphs;

        /// <summary>
        /// A collection of Addressables group definitions associated by their name.
        /// </summary>
        public Dictionary<string, GroupLayout> GroupLayout;

        /// <summary>
        /// A value indicating whether the assets are currently being edited.
        /// </summary>
        public bool AssetEditingInProgress;

        /// <summary>
        /// A reference to the object used to filter logs to the console.
        /// </summary>
        public Logger Logger;
        #endregion
    }
}
