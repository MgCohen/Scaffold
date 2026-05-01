using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace AAGen
{
    /// <summary>
    /// Represents the rules that are used to exclude or transform Group layouts as output in the AAGen pipeline.
    /// </summary>
    public abstract class OutputRule : ScriptableObject
    {
        #region Fields
        /// <summary>
        /// A reference to an Addressables group template.
        /// </summary>
        [SerializeField]
        protected AddressableAssetGroupTemplate m_Template;

        /// <summary>
        /// A reference to the <see cref="DataContainer"/>.
        /// </summary>
        protected DataContainer m_DataContainer;

        /// <summary>
        /// The Addressables group definitions that match the selection criteria.
        /// </summary>
        protected List<GroupLayout> m_Selection;
        #endregion

        #region Methods
        /// <summary>
        /// Handled by the Unity Editor when the object should be ready for consumption.
        /// </summary>
        private void OnValidate()
        {
            // If the reference to the Addressables group template is invalid, then:
            if (m_Template == null)
            {
                // Assign the default template to the reference.
                m_Template = AddressableUtil.FindDefaultAddressableGroupTemplate();
            }
        }

        /// <summary>
        /// Initializes the output rule.
        /// </summary>
        /// <param name="dataContainer">A reference to the <see cref="DataContainer"/>.</param>
        public virtual void Initialize(DataContainer dataContainer)
        {
            m_DataContainer = dataContainer;
        }
        
        /// <summary>
        /// Collect a subset of group layouts that matches the criteria.
        /// </summary>
        public virtual void Select()
        {
            // Get the Addressables group definitions that match the selection criteria.
            // Cache it as a list.
            m_Selection = m_DataContainer.GroupLayout.Values.Where(DoesMatchSelectionCriteria).ToList();
        }
        
        /// <summary>
        /// Refine the selected group layouts.
        /// </summary>
        public virtual void Refine()
        {
            // Apply the template to the selected group layouts.
            ApplyTemplate(m_Selection);
        }

        /// <summary>
        /// Uninitializes the output rule.
        /// </summary>
        public virtual void UnInit()
        {
            m_DataContainer = null;
            m_Selection = null;
        }
        
        /// <summary>
        /// Determines whether a group layout matches the criteria for selection.
        /// </summary>
        /// <param name="groupLayout">The group layout.</param>
        /// <returns>A value indicating that the group layout should be included in the selection.</returns>
        protected abstract bool DoesMatchSelectionCriteria(GroupLayout groupLayout);

        /// <summary>
        /// Merge the collection of selected group layouts into one group layout.
        /// </summary>
        /// <param name="groupLayouts">The collection of selected group layouts,</param>
        protected void Merge(List<GroupLayout> groupLayouts, string mergedGroupName = null)
        {
            // If the group layouts are not valid, or its valid but there are no group layouts, then:
            if (groupLayouts == null || groupLayouts.Count == 0)
            {
                // Do nothing else.
                return;
            }

            // Otherwise, the group layouts are valid and there is at least one.

            // Sanity check:
            // For every group layout in the collection, perform the following:
            foreach (var groupLayout in groupLayouts)
            {
                // If the name of the group layout does not exist in the data container, then it is a group layout that does not belong.
                // If this is a group that does not belong, then:
                if (!m_DataContainer.GroupLayout.ContainsKey(groupLayout.Name))
                {
                    // Log an error with details.
                    Debug.LogError($"Cannot find group layout name = {groupLayout.Name}");

                    // Do nothing else.
                    return;
                }
            }

            // All groups in the collection come from the data container.

            // Create a collection of names of group layouts to remove.
            var keysToRemove = new List<string>();

            // Create a unique set of sub-graph nodes.
            var allNodes = new HashSet<AssetNode>();

            // Create a unique set of sub-graph sources.
            var allSources = new HashSet<AssetNode>();

            // For every group layout in the collection, perform the following:
            foreach (var groupLayout in groupLayouts)
            {
                // Add the name of the group layout to the list of layouts to remove.
                keysToRemove.Add(groupLayout.Name);

                // Add the nodes of the group layout to the unique set of sub-graph nodes.
                allNodes.UnionWith(groupLayout.Nodes);

                // Add the sources of the group layout to the unique set of sub-graph sources.
                allSources.UnionWith(groupLayout.Sources);
            }

            // Generate a hash value for the unique set of source nodes.
            var hashOfAllSources = SubgraphCommandQueue.CalculateHashForSources(allSources);

            // Format a name for a merged group.
            string groupLayoutName = !string.IsNullOrEmpty(mergedGroupName)
                ? mergedGroupName
                : $"Merged_Shared_Assets_{hashOfAllSources}";

            //// If the Addressables group template is valid, then use the template.
            //// Otherwise, the find the defauly template from Addressables settings and use that.
            //var template = m_Template != null ? m_Template : AddressableUtil.FindDefaultAddressableGroupTemplate();
            
            // Create a new group layout with all the merged nodes and sources.
            var mergedGroupLayout = new GroupLayout
            {
                Nodes = allNodes,
                Sources = allSources,
                HashOfSources = hashOfAllSources,
                Name = groupLayoutName,
                //NOTE: should we change the property from 'name' to 'Name'?
                //TemplateName = template.name,
            };
            
            // For each name of a group layout to remove, perform the following:
            foreach (var key in keysToRemove)
            {
                // Remove the group layout from the data container.
                m_DataContainer.GroupLayout.Remove(key);
            }
            
            // Replace all the removed group layouts with the merged group layout.
            m_DataContainer.GroupLayout.Add(mergedGroupLayout.Name, mergedGroupLayout);
            
            // Refresh the selection.
            Select();
        }

        /// <summary>
        /// Apply the group template to the selected group layouts.
        /// </summary>
        /// <param name="groupLayouts">A collection of selected group layouts.</param>
        protected void ApplyTemplate(List<GroupLayout> groupLayouts)
        {
            // If the Addressables group template is valid, then use the template.
            // Otherwise, the find the defauly template from Addressables settings and use that.
            AddressableAssetGroupTemplate template = m_Template != null ?
                m_Template : AddressableUtil.FindDefaultAddressableGroupTemplate();
            
            // For every group layout in the collection, perform the following:
            foreach (var groupLayout in groupLayouts)
            {
                // Use the group template to define what the layout uses to build Addressables groups.
                groupLayout.TemplateName = template.name;
            }
        }

        /// <summary>
        /// Attempt to rename a group layout.
        /// </summary>
        /// <param name="groupLayout">The group layout to rename.</param>
        /// <param name="newName">The new name of the group layout.</param>
        protected void Rename(GroupLayout groupLayout, string newName)
        {
            // If the group layouts are not valid, or the new name is invalid or empty, then:
            if (groupLayout == null || string.IsNullOrEmpty(newName))
            {
                // Do nothing else.
                return;
            }

            // Otherwise, the group layouts are valid and the name is valid and non-empty.

            // Cache the current name of the group layout.
            var oldName = groupLayout.Name;

            // Sanity check:
            // If the name of the group layout does not exist in the data container, then it is a group layout that does not belong.
            // If this is a group that does not belong, then:
            if (!m_DataContainer.GroupLayout.ContainsKey(oldName))
            {
                // Log an error with details.
                Debug.LogError($"Cannot find group layout name = {oldName}");

                // Do nothing else.
                return;
            }

            // Otherwise, the group layout comes from the data container.

            // If the current name and the new name are identical, then there should be no rename.
            // If there should be no rename, then:
            if (oldName.Equals(newName))
            {
                // Do nothing else.
                return;
            }

            // Otherwise, there should be a rename.
            
            // If the new name is already associated with a group layout in the data container, then:
            if (m_DataContainer.GroupLayout.ContainsKey(newName))
            {
                // Log an error with details.
                Debug.LogError($"{newName} is not available!");

                // Do nothing else.
                return;
            }

            // Otherwise, the new name is not already associated with the group layout in the data container.
            
            // Disassociate the group layout with its current name.
            m_DataContainer.GroupLayout.Remove(oldName);
            
            // Cache the new name as the group layout name.
            groupLayout.Name = newName;

            // Associate the group lauout with the new name.
            m_DataContainer.GroupLayout.Add(groupLayout.Name, groupLayout);
        }
        #endregion
    }
}
