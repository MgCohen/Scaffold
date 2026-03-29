using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace AAGen
{
    /// <summary>
    /// Represents a command queue that creates group layout definitions.
    /// </summary>
    internal class GroupLayoutCommandQueue : CommandQueue
    {
        #region Static Methods
        /// <summary>
        /// Generate the name of the Addressables group from a sub-graph.
        /// </summary>
        /// <param name="subgraph">The sub-graph.</param>
        /// <returns>The name of the Addressables group.</returns>
        /// NOTE: To Util Method
        public static string GetDefaultGroupName(Subgraph subgraph)
        {
            // Get the hash that represents the unique properties of all source nodes of the sub-graph.
            return subgraph.HashOfSources.ToString();
        }
        #endregion

        #region Fields
        /// <summary>
        /// A reference to the <see cref="DataContainer"/>.
        /// </summary>
        private readonly DataContainer m_DataContainer;

        /// <summary>
        /// A reference to the default Addressables group template.
        /// </summary>
        private AddressableAssetGroupTemplate m_DefaultTemplate;
        #endregion

        #region Methods
        /// <summary>
        /// Creates a new instance of the <see cref="GroupLayoutCommandQueue"/> class.
        /// </summary>
        /// <param name="dataContainer">A reference to the <see cref="DataContainer"/>.</param>
        public GroupLayoutCommandQueue(DataContainer dataContainer)
        {
            m_DataContainer = dataContainer;

            Title = nameof(GroupLayoutCommandQueue);
        }

        /// <summary>
        /// Performs an action before processing the commands in the queue.
        /// </summary>
        public override void PreExecute()
        {
            m_DataContainer.GroupLayout = new Dictionary<string, GroupLayout>();

            // Cache a reference to the default Addressables group template.
            m_DefaultTemplate = AddressableUtil.FindDefaultAddressableGroupTemplate();

            // Clear the command queue.
            ClearQueue();

            // For each sub-graph that was generated, perform the following:
            foreach (var pair in m_DataContainer.Subgraphs)
            {
                // Create a localized cache for the sub-graph,
                // and the hash that represents the unique properties of all its source nodes
                // (so that it can be properly captured by the lambda).
                var hash = pair.Key;
                var subgraph = pair.Value;

                // Add a command that creates the group layout for this sub-graph.
                AddCommand(() => CreateGroupLayout(subgraph), hash.ToString());
            }
        }
        
        /// <summary>
        /// Performs an action after processing the commands in the queue.
        /// </summary>
        public override void PostExecute()
        {
            SaveOutputReportToFile();
        }

        /// <summary>
        /// Creates a new instance of the <see cref="GroupLayout"/> and adds it to the global collection.
        /// </summary>
        /// <param name="subgraph">The sub-graph the group layout is defined from.</param>
        /// <exception cref="Exception">Throws an error if the sub-graph has no nodes.</exception>
        private void CreateGroupLayout(Subgraph subgraph)
        {
            // Create a new instance of a group layout, using relevant information about the sub-graph.
            var groupLayoutInfo = new GroupLayout
            {
                Nodes = subgraph.Nodes,
                Sources = subgraph.Sources,
                HashOfSources = subgraph.HashOfSources,
                Name = GetDefaultGroupName(subgraph),
                TemplateName = m_DefaultTemplate.Name,
            };

            // NOTE: Sanity check.
            // If the group layout has no nodes, then it will defines an Addressables group without assets.
            // If the layout group defines an Addressables group without assets, then:
            if (groupLayoutInfo.Nodes.Count == 0)
            {
                // Throw an error with the details.
                throw new Exception($"group node count == 0!");
            }

            // Otherwise, the group layout defines an Addressables group with assets.

            // Add the group layout to the collection of layouts.
            m_DataContainer.GroupLayout.Add(groupLayoutInfo.Name, groupLayoutInfo);
        }
        
        void SaveOutputReportToFile()
        {
            if (!m_DataContainer.Settings.ProcessReport.HasFlag(ProcessStepReport.GroupLayout))
                return;

            var summary = $"(GroupLayout.Count = {m_DataContainer.GroupLayout.Count})";
            var data = m_DataContainer.GroupLayout;

            JsonReport.SaveJsonReport(GetType(), summary, data);
        }
        #endregion
    }
}