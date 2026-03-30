using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace AAGen
{
    /// <summary>
    /// Represents a command queue that identifies sub-graphs in the dependency graph.
    /// </summary>
    internal class SubgraphCommandQueue : CommandQueue
    {
        #region Static Methods
        /// <summary>
        /// Generate a hash value for the unique set of source nodes.
        /// </summary>
        /// <param name="sources">The unique set of source nodes.</param>
        /// <returns>A hash value for the unique set of source nodes.</returns>
        /// NOTE: Should this be moved into its own utility class?
        public static int CalculateHashForSources(IEnumerable<AssetNode> sources)
        {
            unchecked
            {
                const uint seed = 2166136261u; // FNV-1a 32-bit seed
                const uint prime = 16777619u;

                uint hash = seed;

                foreach (var guid in sources
                             .Select(s => s.Guid.ToString()) // Unity GUID is always 32 hex chars
                             .OrderBy(g => g, StringComparer.Ordinal))
                {
                    // FNV-1a over the canonical GUID string
                    for (int i = 0; i < guid.Length; i++)
                    {
                        hash ^= guid[i];
                        hash *= prime;
                    }
                }

                return (int)hash;
            }
        }
        #endregion

        #region Fields
        /// <summary>
        /// A reference to the <see cref="DataContainer"/>.
        /// </summary>
        private readonly DataContainer m_DataContainer;

        private Graph<AssetNode> m_TransposedGraph;

        /// <summary>
        /// A directed acyclic graph.
        /// </summary>
        private Graph<SuperNode> m_Dag;

        private Graph<SuperNode> m_TransposedDag;

        private Dictionary<AssetNode, SuperNode> m_NodeToSuperNodeMap;
        Dictionary<SuperNode, HashSet<AssetNode>> m_SuperNodeToSources;
        Dictionary<SuperNode, HashSet<AssetNode>> m_SuperNodeToPathAssets;
        
        HashSet<AssetNode> m_InputAssets;
        
        #endregion

        #region Methods
        /// <summary>
        /// Creates a new instance of the <see cref="SubgraphCommandQueue"/> class.
        /// </summary>
        /// <param name="dataContainer">A reference to the <see cref="DataContainer"/>.</param>
        public SubgraphCommandQueue(DataContainer dataContainer)
        {
            m_DataContainer = dataContainer;

            Title = nameof(SubgraphCommandQueue);
        }

        /// <summary>
        /// Performs an action before processing the commands in the queue.
        /// </summary>
        public override void PreExecute()
        {
            // Clear the command queue.
            ClearQueue();
            
            // Create a new instance of a collection of sub-graphs associated by their unique set of source nodes.
            m_DataContainer.Subgraphs = new Dictionary<int, Subgraph>();
            var nodes = m_DataContainer.DependencyGraph.GetAllNodes();
            InitializeDag();
            
            // Add async commands: Get all nodes that define the dependency graph.
            var info = "Materializing SuperNode Maps... ";
            foreach (var s in m_Dag.GetAllNodes())
            {
                var localS = s;
                AddCommand(() => MapSuperNode(localS), info);
            }
            
            // For each node in the dependency graph, perform the following:
            foreach (var node in nodes)
            {
                // Create a localized cache for the node (so that it can be properly captured by the lambda).
                var localNode = node;

                // Add a command that will add the node to the sub-graph with the same source nodes. 
                AddCommand(() => AddNodeToSubgraph(localNode), localNode.FileName);
            }
        }
        
        /// <summary>
        /// Performs an action after processing the commands in the queue.
        /// </summary>
        public override void PostExecute()
        {
            SaveOutputReportToFile();
            UnInitialize();
        }

        private void InitializeDag()
        {
             m_InputAssets = m_DataContainer.InputAssets.Select(AssetNode.FromAssetPath).ToHashSet();
            
            m_Dag = m_DataContainer.DependencyGraph.CollapseToSCCs();
            m_TransposedDag = m_Dag.GetTransposedGraph();
            
            // Initialize Maps
            // which AssetNode belongs to which SuperNode 
            m_NodeToSuperNodeMap = new();
            m_SuperNodeToSources = new Dictionary<SuperNode, HashSet<AssetNode>>();
            m_SuperNodeToPathAssets = new Dictionary<SuperNode, HashSet<AssetNode>>();
            
            // Materialize AssetNode -> SuperNode map
            foreach (var superNode in m_Dag.GetAllNodes())
            {
                foreach (var asset in superNode.Nodes)
                {
                    m_NodeToSuperNodeMap[asset] = superNode;
                }
            }
        }

        /// <summary>
        /// Release temporary memory
        /// </summary>
        void UnInitialize()
        {
            m_InputAssets = null;
            m_Dag = null;
            m_TransposedDag = null;
            m_NodeToSuperNodeMap = null;
            m_SuperNodeToSources = null;
            m_SuperNodeToPathAssets = null;
        }

        /// <summary>
        /// Finds the sources and paths to them for a given super node, and caches the info.
        /// </summary>
        /// <param name="s"></param>
        void MapSuperNode(SuperNode s)
        {
            m_TransposedDag.FindPathAndLeaves(s, out var path, out var superSources);
            
            var pathAssets = GetAllAssetNodes(path);         
            var sourcesAssets = GetAllAssetNodes(superSources);

            m_SuperNodeToPathAssets.Add(s, pathAssets);
            m_SuperNodeToSources.Add(s, sourcesAssets);
        }

        /// <summary>
        /// Attempt to add the node to a sub-graph that has the same set of source nodes.
        /// </summary>
        /// <param name="node">The node to add.</param>
        /// <exception cref="Exception">Throws errors if sanity checking fails.</exception>
        private void AddNodeToSubgraph(AssetNode node)
        {
            // Attempt to find valid source nodes associated with the node.
            bool sourceFound = TryFindSourcesForNode_DAG(node, out var sources);

            // If no valid source nodes are associated with the node, then:
            if (!sourceFound)
            {
                // Do nothing else.
                return;
            }

            // Otherwise, there are valid source nodes associated with the node.

            // NOTE: Is this check even necessary? we already check for this in TryFindSourcesForNode
            // If for whatever reason the container is invalid or empty, then:
            if (sources == null || sources.Count == 0)
            {
                // Throw an error with the details. 
                throw new Exception($"Cannot find source nodes for node = {node.FileName}");
            }

            // Otherwise, there are valid source nodes associated with the node.

            // Generate a hash value for the unique set of source nodes.
            int hash = CalculateHashForSources(sources);

            // Attempt to find a subgraph associated with the unique set of source nodes.
            // If one exists, then:
            if (m_DataContainer.Subgraphs.TryGetValue(hash, out var existingSubgraph))
            {
                // Sanity check that the subgraph that was found has the same sources
                // If the subgraph does not have these same exact sources, then: 
                if (!existingSubgraph.Sources.SetEquals(sources))
                {
                    // Throw an error with the details.
                    throw new Exception($"Hash collision = inconsistent sources for subgraph {hash}");
                }
            }
            else
            {
                // Otherwise, there is no subgraph associated with the unique set of source nodes.

                // Create a new sub-graph with the sources and the hash. 
                var newSubgraph = new Subgraph
                {
                    Sources = sources,
                    HashOfSources = hash
                };
                
                // Associate the new sub-graph with the unique sources.
                m_DataContainer.Subgraphs.Add(hash, newSubgraph);
            }

            // There exists a sub-graph associated with the unique set of source nodes.
            // NOTE: code can be streamlined to use the same variable instead of
            // existingSubgraph, newSubgraph, and m_DataContainer.Subgraphs[hash] 

            // Add the node to the sub-graph.
            // Cache the value indicating that a new node was inserted to the set. 
            bool result = m_DataContainer.Subgraphs[hash].Nodes.Add(node);

            // Sanity checking to make sure the node isn't added before. If so, it can indicate a problem in our logic.
            
            // If the node was not added, then there was duplicate processing on the same node.
            if (!result)
            {
                // Throw an error with the details.
                throw new Exception($"Unknown Error = node = {node} had added to subgraph ={hash} before");
            }
        }

        private bool TryFindSourcesForNode_DAG(AssetNode targetNode, out HashSet<AssetNode> sources)
        {
            // Get the SuperNode for your target AssetNode
            if (!m_NodeToSuperNodeMap.TryGetValue(targetNode, out var targetSuperNode))
            {
                throw new InvalidOperationException("AssetNode not found in collapsed DAG.");
            }
            
            if (m_SuperNodeToPathAssets[targetSuperNode].Overlaps(m_InputAssets))
            {
                sources = m_SuperNodeToSources[targetSuperNode]; // no new allocation
                return true;
            }
            sources = null;
            return false;
        }

        private HashSet<AssetNode> GetAllAssetNodes(HashSet<SuperNode> superNodes)
        {
            var assetNodes = new HashSet<AssetNode>();
            
            foreach (var superNode in superNodes)  
            {
                assetNodes.UnionWith(superNode.Nodes);
            }
            
            return assetNodes;
        }

        void SaveOutputReportToFile()
        {
            if (!m_DataContainer.Settings.ProcessReport.HasFlag(ProcessStepReport.SubGraphs))
                return;
            
            var summary = $"Subgraphs.Count = {m_DataContainer.Subgraphs.Count} ";
            var data = m_DataContainer.Subgraphs;

            JsonReport.SaveJsonReport(GetType(), summary, data);
        }

        #endregion
    }
}
