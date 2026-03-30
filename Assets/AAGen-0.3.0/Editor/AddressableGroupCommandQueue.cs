using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace AAGen
{
    /// <summary>
    /// Represents a command queue that processes group layouts into Addressables groups. 
    /// </summary>
    internal class AddressableGroupCommandQueue : CommandQueue
    {
        #region Fields
        /// <summary>
        /// A reference to the <see cref="DataContainer"/>.
        /// </summary>
        private readonly DataContainer m_DataContainer;

        /// <summary>
        /// The number of Addressables groups that are created.
        /// </summary>
        private int m_AddressableGroupCreated;

        /// <summary>
        /// The number of Addressables groups that are reused.
        /// </summary>
        private int m_AddressableGroupReused;

        Dictionary<string, AddressableAssetGroup> m_ExistingGroups;
        Dictionary<string, AddressableAssetGroupTemplate> m_Templates;
        #endregion

        #region Properties
        /// <summary>
        /// Get the reference to the Addressables settings.
        /// </summary>
        private AddressableAssetSettings AddressableSettings => AddressableAssetSettingsDefaultObject.Settings;
        #endregion

        #region Methods
        /// <summary>
        /// Creates a new instance of the <see cref="AddressableGroupCommandQueue"/> class.
        /// </summary>
        /// <param name="dataContainer">A reference to the <see cref="DataContainer"/>.</param>
        public AddressableGroupCommandQueue(DataContainer dataContainer)
        {
            m_DataContainer = dataContainer;

            Title = nameof(AddressableGroupCommandQueue);
        }

        /// <summary>
        /// Performs an action before processing the commands in the queue.
        /// </summary>
        public override void PreExecute()
        {
            // Clear the command queue.
            ClearQueue();
            
            m_ExistingGroups = AddressableSettings.groups.ToDictionary(k => k.Name, v => v);
            m_Templates = AddressableSettings.GroupTemplateObjects.ToDictionary(k => k.name, v => (AddressableAssetGroupTemplate)v);

            // Add a command to configure Unity for editing many assets.
            AddCommand(StartAssetEditing);
            
            // For each group layout, perform the following:
            foreach (var pair in m_DataContainer.GroupLayout)
            {
                // Create a localized cache for the group layout and its name
                // (so that it can be properly captured by the lambda).
                var groupName = pair.Key;
                var groupLayoutInfo = pair.Value;

                // Add a command for creating an Addressables Group and moving assets.
                AddCommand(() => CreateGroupAndMoveAssets(groupName, groupLayoutInfo), groupName);
            }

            // Add a command to set Unity back to efore many asset edits.
            AddCommand(StopAssetEditing);
        }
        
        /// <summary>
        /// Performs an action after processing the commands in the queue.
        /// </summary>
        public override void PostExecute()
        {
            SaveOutputReportToFile();
            
            //UnInitialize
            m_ExistingGroups = null;
            m_Templates = null;
        }

        /// <summary>
        /// Start editing assets.
        /// </summary>
        private void StartAssetEditing()
        {
            // Ensure that the files that were saved are updated in the Unity Editor's project UI.
            AssetDatabase.Refresh();

            // Pause the asset importer so that any assets created do not immediately automatically import.
            AssetDatabase.StartAssetEditing();

            // Let the rest of the AAGen system know that the assets are being edited.
            m_DataContainer.AssetEditingInProgress = true;
        }

        /// <summary>
        /// Define the Addressables group fro, the group layout.
        /// </summary>
        /// <param name="groupName">The name of the Addressables group.</param>
        /// <param name="groupLayout">The group layout.</param>
        /// <exception cref="Exception">Throws errors if assets were invalid or they failed to be added to the group.</exception>
        private void CreateGroupAndMoveAssets(string groupName, GroupLayout groupLayout)
        {
            // Attempt to retrieve a group by the name that it is associated with.
            // If there is a group associated with that name, then:
            if (m_ExistingGroups.TryGetValue(groupName, out AddressableAssetGroup group))
            {
                m_AddressableGroupReused++;
            }
            else
            {
                // Otherwise, there is no group associated with that name.

                // Add a new Addressables group with that name to the Addressables.
                group = CreateNewGroup(groupName, groupLayout.TemplateName);

                // Increment the number of Addressables groups that are created by one.
                m_AddressableGroupCreated++;
            }
            
            // For every group layout node, perform the following:
            foreach (var node in groupLayout.Nodes)
            {
                // Get the GUID that is registered with an asset in the AssetDatabase.
                string assetGuid = node.Guid.ToString();

                // If there is no valid asset GUID, then:
                if (string.IsNullOrEmpty(assetGuid))
                {
                    // Throw an error with the details.
                    throw new Exception($"Asset with path '{node.AssetPath}' not found in project.");
                }

                // Otherwise, the asset GUID is valid.

                // Add the asset as an entry to the Addressables group.
                AddressableAssetEntry entry = AddressableSettings.CreateOrMoveEntry(assetGuid, group, false, false);

                // If the entry is inavlid, then the asset was not added to the Addressables group.
                // If the asset was not added to the Addressables group, then:
                if (entry == null)
                {
                    // Throw an error with the details.
                    throw new Exception($"Failed to add asset '{node.AssetPath}' to group '{group.name}'.");
                }
            }
        }

        /// <summary>
        /// Adds a new Addressables group to the Addressables system.
        /// </summary>
        /// <param name="name">The name of the Addressables group.</param>
        /// <returns>The Addressables group instance.</returns>
        private AddressableAssetGroup CreateNewGroup(string name, string templateName)
        {
            List<AddressableAssetGroupSchema> schemasToCopy = null;
            if (m_Templates.TryGetValue(templateName, out AddressableAssetGroupTemplate template))
                schemasToCopy = template.SchemaObjects;
                
            // Adds the group to Addressables with a BundledAssetGroupSchema attached.
            return AddressableSettings.CreateGroup(name, false, false,
                false, schemasToCopy, Type.EmptyTypes);
        }

        /// <summary>
        /// Applies a group template values to the Addressables group.
        /// </summary>
        /// <param name="group">An instance of the Addressables group.</param>
        /// <param name="templateName">The name of the Addressables group template.</param>
        private void ApplyTemplateValuesToGroup(AddressableAssetGroup group, string templateName)
        {
            if (m_Templates.TryGetValue(templateName, out AddressableAssetGroupTemplate template))
                template.ApplyToAddressableAssetGroup(group);
        }

        /// <summary>
        /// Stop editing assets.
        /// </summary>
        private void StopAssetEditing()
        {
            // Resume the asset importer so that all assets created are immediately automatically import.
            AssetDatabase.StopAssetEditing();

            // Let the rest of the AAGen system know that the assets are no longer being edited.
            m_DataContainer.AssetEditingInProgress = false;

            // Ensure that the files that were saved are updated in the Unity Editor's project UI.
            AssetDatabase.Refresh();
        }
        
        void SaveOutputReportToFile()
        {
            if (!m_DataContainer.Settings.ProcessReport.HasFlag(ProcessStepReport.AddressableGroups))
                return;
            
            var summary = string.Empty;
            summary += $"{nameof(m_AddressableGroupCreated).ToReadableFormat()} = {m_AddressableGroupCreated}, ";
            summary += $"{nameof(m_AddressableGroupReused).ToReadableFormat()} = {m_AddressableGroupReused}";
            object data = string.Empty; //reserved

            JsonReport.SaveJsonReport(GetType(), summary, data);
        }
        #endregion
    }
}