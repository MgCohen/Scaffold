using System;
using System.Collections.Generic;
using UnityEditor;

namespace AAGen
{
    /// <summary>
    /// Represents a graph that encapsulates asset relationship data.
    /// </summary>
    public class DependencyGraph : Graph<AssetNode>
    {
        #region Types
        /// <summary>
        /// Represents a <see cref="DependencyGraph"/> that can be serialized in a sparse manner.
        /// </summary>
        [Serializable]
        public class SerializedData
        {
            #region Fields
            /// <summary>
            /// A graph where the nodes store indices to asset GUIDs.
            /// </summary>
            public Graph<int> Graph = new();

            /// <summary>
            /// A collection of sparse indices associated with the asset GUIDs.
            /// </summary>
            public Dictionary<string, int> IndexDictionary = new();
            #endregion
        }
        #endregion

        #region Static Methods
        /// <summary>
        /// Take the deserialize the memento state into an instance of <see cref="DependencyGraph"/>.
        /// </summary>
        /// <param name="serializedData"></param>
        /// <returns>A new instance of the dependency graph that was created from the memento state.</returns>
        public static DependencyGraph Deserialize(SerializedData serializedData)
        {
            // Create a collection of asset GUIDs associated with the index.
            var invertedDictionary = new Dictionary<int, string>();

            // For every asset GIUD associated and its unique index, perform the following:
            foreach ((string assetGUID, int index) in serializedData.IndexDictionary)
            {
                // Invert the serialized dictionary so that the index is the lookup key.
                invertedDictionary.Add(index, assetGUID);
            }

            // Convert the memento state graph into a fill fledged graph.
            Graph<AssetNode> graph = serializedData.Graph.ConvertNodeType(ConvertIndexToGuid);

            // Pack into a dependency graph instance.
            return new DependencyGraph(graph);

            AssetNode ConvertIndexToGuid(int nodeIndex)
            {
                // Get the asset GUID associated with the index.
                var guidString = invertedDictionary[nodeIndex];

                // Create a node using the formatted GUID string.
                // Return the created node.
                return AssetNode.FromGuidString(guidString);
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Create a new instance of the <see cref="DependencyGraph"/> class.
        /// </summary>
        public DependencyGraph()
        {
        }

        /// <summary>
        /// Create a new instance of the <see cref="DependencyGraph"/> class.
        /// </summary>
        /// <param name="graph">The other graph to copt from.</param>
        public DependencyGraph(Graph<AssetNode> graph)
        {
            // Configure this graph instance from the other graph.
            FromGraph(graph);
        }

        /// <summary>
        /// An a directed edge from the source asset to the destination asset.
        /// </summary>
        /// <param name="pathA">The file path of the source asset.</param>
        /// <param name="pathB">The file path of the destination asset.</param>
        public void AddEdge(string pathA, string pathB)
        {
            // Convert file paths of the asset to their guid they have been registered with in the asset database.
            var guidA = AssetDatabase.GUIDFromAssetPath(pathA);
            var guidB = AssetDatabase.GUIDFromAssetPath(pathB);

            // Create a new asset nodes and add an edge from the source to the destination.
            base.AddEdge(new AssetNode(guidA), new AssetNode(guidB));
        }
        
        /// <summary>
        /// Add a node to the dependency graph.
        /// </summary>
        /// <param name="path">The file path of the asset to add as a node.</param>
        public void AddNode(string path)
        {
            // Convert file path of the asset to the guid it has been registered with in the asset database.
            var guid = AssetDatabase.GUIDFromAssetPath(path);

            // Create a new asset node and add it to the graph.
            base.AddNode(new AssetNode(guid));
        }
        
        /// <summary>
        /// Gets the number of edges that emit from a node.
        /// </summary>
        /// <param name="node">The node that is the source of the edges.</param>
        /// <returns>The number of edges that the emit from the node.</returns>
        public int CountOutgoingEdges(AssetNode node)
        {
            // Get number of nodes that are adjacent to the node that was passed in.
            return GetNeighbors(node).Count;
        }

        /// <summary>
        /// Configure the contents of this graph using another graph as the source.
        /// </summary>
        /// <param name="graph">The source graph.</param>
        private void FromGraph(Graph<AssetNode> graph)
        {
            // Reset the adjacency list.
            _adjacencyList = new Dictionary<AssetNode, List<AssetNode>>();

            // For each node in the other graph, perform the following:
            foreach (var node in graph.GetAllNodes())
            {
                // Add the node and its neighbors to the adjacency list.
                _adjacencyList.Add(node, graph.GetNeighbors(node));
            }
        }
        
        /// <summary>
        /// Serializes this object into a memento state.
        /// </summary>
        /// <returns>An instance of the memento state.</returns>
        public SerializedData Serialize()
        {
            // Create a new instance of serialization data.
            var serializedData = new SerializedData();

            // Start the index at zero.
            int index = 0;
            
            // Take this graph and convert the data of each node into one that has stored the sparse index.
            // Assign the translated graph to the instance of the serialized data.
            serializedData.Graph = ConvertNodeType(ConvertGuidToIndex);

            // Return the serialized data as the result.
            return serializedData;
            
            int ConvertGuidToIndex(AssetNode node)
            {
                // Get the node's asset GUID to a formatted string.
                var guidString = node.Guid.ToString();

                // Attempt to get the recorded index associated with asset GUID.
                // If there is one, then:
                if (serializedData.IndexDictionary.TryGetValue(guidString, out int recordedIndex))
                {
                    // Return the recorded index as value of the new node type.
                    return recordedIndex;
                }

                // Otherwise, there is no index associated with the asset GUID.

                // The next index is needed; increment to the next index.
                index++;

                // Associate the next index with the asset GUID.
                serializedData.IndexDictionary.Add(guidString, index);

                // Return the new index.
                return index;
            }
        }
        #endregion
    }
}
