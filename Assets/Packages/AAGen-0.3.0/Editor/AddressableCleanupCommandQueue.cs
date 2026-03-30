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
    /// Represents a command queue that sanitizes the Addressables system to be in sync with AAGen definitions.
    /// </summary>
    internal class AddressableCleanupCommandQueue : CommandQueue
    {
        #region Static Methods
        /// <summary>
        /// Get a unique set of file paths to Addressables scene assets.
        /// </summary>
        /// <returns></returns>
        private static HashSet<string> GetAddressableScenePaths()
        {
            // Create a unique set, which represents a the file paths of Addressables assets.
            var paths = new HashSet<string>();

            // Get a reference to the Addressables settings instance.
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            // If the Addressables settings is invalid, then:
            if (settings == null)
            {
                // Return an empty but valid set of asset file paths.
                return paths;
            }

            // For each Addressables asset entry in the Addressables settings, perform the following:
            foreach (var entry in AddressableUtil.GetAddressableEntries())
            {
                // If an main asset type of the Addressables entry is a scene, then:
                if (entry.MainAssetType == typeof(SceneAsset))
                {
                    // Get the file path associated with the asset.
                    string assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);

                    // If the file path is valid or non-empty, then:
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        // Add the file path to the asset to the list of file paths.
                        paths.Add(assetPath);
                    }
                }
            }

            return paths;
        }
        #endregion
        
        #region Fields
        /// <summary>
        /// A reference to the <see cref="DataContainer"/>.
        /// </summary>
        private readonly DataContainer m_DataContainer;

        /// <summary>
        /// The number of empty groups removed.
        /// </summary>
        private int m_EmptyGroupRemoved;

        /// <summary>
        /// The number of entries that were removed.
        /// </summary>
        private int m_UnnecessaryEntriesRemoved;

        HashSet<string> m_AddressableScenes;

        List<string> m_RemovedEntriesReport;
        List<string> m_RemovedGroupsReport;
        #endregion

        #region Properties
        /// <summary>
        /// Get the reference to the Addressables settings.
        /// </summary>
        private AddressableAssetSettings AddressableSettings => AddressableAssetSettingsDefaultObject.Settings;

        bool IsReportEnabled => m_DataContainer.Settings.ProcessReport.HasFlag(ProcessStepReport.Cleanup);
        #endregion

        #region Methods
        /// <summary>
        /// Creates a new instance of the <see cref="AddressableCleanupCommandQueue"/> class.
        /// </summary>
        /// <param name="dataContainer">A reference to the <see cref="DataContainer"/>.</param>
        public AddressableCleanupCommandQueue(DataContainer dataContainer)
        {
            m_DataContainer = dataContainer;

            Title = nameof(AddressableCleanupCommandQueue);
        }

        /// <summary>
        /// Performs an action before processing the commands in the queue.
        /// </summary>
        public override void PreExecute()
        {
            // Clear the command queue.
            ClearQueue();

            // Add a command to configure Unity for editing many assets.
            AddCommand(StartAssetEditing);

            // Add a command that removes asset entries that are not being used.
            AddCommand(RemoveUnusedEntries);

            // Add a command that removes Addressables Groups that are empty.
            AddCommand(RemoveEmptyAddressableGroups);

            // Add a command that removes the scenes that are Addressable from the build profile.
            AddCommand(RemoveAddressableScenesFromBuildProfile);

            // Add a command that sorts the Addressables groups in Addressables settings.
            AddCommand(SortGroups);

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
            m_RemovedEntriesReport = null;
            m_RemovedGroupsReport = null;
            m_AddressableScenes = null;
        }

        /// <summary>
        /// Start editing assets.
        /// </summary>
        private void StartAssetEditing()
        {
            // Pause the asset importer so that any assets created do not immediately automatically import.
            AssetDatabase.StartAssetEditing();

            // Let the rest of the AAGen system know that the assets are being edited.
            m_DataContainer.AssetEditingInProgress = true;
        }

        /// <summary>
        /// Removes Addressables Groups that are empty.
        /// </summary>
        private void RemoveUnusedEntries()
        {
            // If AAGen should not remove Addressables entries that are unnecessary, then:
            if (!m_DataContainer.Settings.RemoveUnnecessaryEntries)
            {
                // Do nothing else.
                return;
            }

            // Otherwise, AAGen should remove Addressables entries that are unnecessary.

            // Create a unique set of guid associated with an asset in the AssetDatabase,
            // which represents the assets that are defined in the group layouts.
            var allNodesInGroupLayouts = new HashSet<string>();

            // For every group layout that was generated, perform the following:
            foreach (var groupLayout in m_DataContainer.GroupLayout.Values)
            {
                // For each node in the group layout, perform the following:
                foreach (var node in groupLayout.Nodes)
                {
                    // Add the asset guid to the set.
                    allNodesInGroupLayouts.Add(node.Guid.ToString());
                }
            }
            
            // Find entries that aren't included in group layout
            var entriesToRemove = new List<string>();
            m_RemovedEntriesReport = new List<string>();

            // For each Addressables asset entry in the Addressables settings, perform the following:
            foreach (var entry in AddressableUtil.GetAddressableEntries())
            {
                // Get the guid associated with an asset in the AssetDatabase.
                var entryGuid = entry.guid;
                    
                // If the asset is not defined in the group layouts,
                // then it is an asset that was not added to Addressables by AAGen.
                // If the Addressables entry was not added by AAGen, then:
                if(!allNodesInGroupLayouts.Contains(entryGuid))
                {
                    // Add the Addressables asset entry to the be removed.
                    entriesToRemove.Add(entryGuid);
                    
                    if(IsReportEnabled)
                        m_RemovedEntriesReport.Add(entry.AssetPath);
                }
            }

            // For every Addressables asset entry to remove, perform the following:
            foreach (var guid in entriesToRemove)
            {
                // Remove the entry from the Addressables system.
                AddressableSettings.RemoveAssetEntry(guid, false);

                // Increment the number of entries that were removed by one.
                m_UnnecessaryEntriesRemoved++;
            }
        }

        /// <summary>
        /// Attempt to remove empty Addressables groups.
        /// </summary>
        private void RemoveEmptyAddressableGroups()
        {
            // If AAGen should not clean up Addressables groups that are empty, then:
            if (!m_DataContainer.Settings.RemoveEmptyGroups)
            {
                // Do nothing else.
                return;
            }

            // Otherwise. AAGen should clean up Addressables groups that are empty.

            // For each group, compile the ones that should be removed.
            IEnumerable<AddressableAssetGroup> groupsToRemove = AddressableSettings.groups
                .Where(CanRemoveGroup).ToList();

            if (IsReportEnabled)
                m_RemovedGroupsReport = groupsToRemove.Select(g => g.Name).ToList();

            // For each group to remove, performed the following:
            foreach (var group in groupsToRemove)
            {
                // Remove the Addressables group.
                // AddressableSettings.RemoveGroup(group); //This Addressable public method is extremely slow
            
                RemoveGroupQuick(group); 

                // Increment the number of empty groups removed by one.
                m_EmptyGroupRemoved++;
            }
        }
        
        /// <summary>
        /// Modified Addressable RemoveGroupInternal() that deletes the asset but doesn't post the event 
        /// </summary>
        /// <param name="group"></param>
        void RemoveGroupQuick(AddressableAssetGroup group)
        {
            if (group == null)
                return;
            
            group.ClearSchemas(true);
            AddressableSettings.groups.Remove(group);

            if (group != null)
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(group, out var guidOfGroup, out long localId))
                {
                    var groupPath = AssetDatabase.GUIDToAssetPath(guidOfGroup);
                    if (!string.IsNullOrEmpty(groupPath))
                        AssetDatabase.DeleteAsset(groupPath);
                }
            }
        }

        /// <summary>
        /// Determines whether an Addressables group is valid for removal.
        /// </summary>
        /// <param name="group">An Addressables group.</param>
        /// <returns>A value indicating whether the Addressables group should be removed.</returns>
        private bool CanRemoveGroup(AddressableAssetGroup group)
        {
            // If the group has no entries and is not a read-only group and is not the default group (which must always exist).
            return group != null &&
                   group.entries.Count == 0 &&
                   !group.ReadOnly &&
                   group != AddressableSettings.DefaultGroup;
        }
        
        /// <summary>
        /// Remove the scenes that are Addressable from the build profile.
        /// </summary>
        private void RemoveAddressableScenesFromBuildProfile()
        {
            // If AAGen should not remove the scenes that are Addressable from the build profile, then:
            if (!m_DataContainer.Settings.RemoveAddressableScenesFromBuildProfile)
            {
                // Do nothing else.
                return;
            }

            // Otherwise, AAGen should remove the scenes that are Addressable from the build profile.

            // Get a unique set of file paths to Addressables scene assets.
            m_AddressableScenes = GetAddressableScenePaths();

            // Get the list of scenes in the build profile.
            List<EditorBuildSettingsScene> originalScenes = EditorBuildSettings.scenes.ToList();

            // The number of scenes removed from the build profiles. Defaults to zero.
            int removedCount = 0;

            // Create a list of updated scenes for the build profile.
            var updatedScenes = new List<EditorBuildSettingsScene>();

            // For every scene in the build profile, perform the following:
            foreach (EditorBuildSettingsScene scene in originalScenes)
            {
                // If the file path of the scene is not in the set of file paths to Addressable scene assets,
                // then the scene is not an Addressable asset.
                // If the scene is not an Addressable asset, then:
                if (!m_AddressableScenes.Contains(scene.path))
                {
                    // Add the scene to the unique set of scenes in the 
                    updatedScenes.Add(scene);
                }
                else
                {
                    // Otherwise, the scene is an Addressable asset.

                    // Do not add the Addressable scene to the updated list.

                    // Increment the number of scenes removed from the build profile.
                    removedCount++;
                }
            }

            // If the number of scenes removed from the build profile is positive non-zero,
            // then there were scenes removed from the build profile.
            // If there were scenes removed from the build profile, then:
            if (removedCount > 0)
            {
                // Update the scenes in the build profile so that they consist of the scenes that are not Addressables.
                EditorBuildSettings.scenes = updatedScenes.ToArray();
            }
        }

        /// <summary>
        /// Sorts the Addressables groups in Addressables settings.
        /// </summary>
        private void SortGroups()
        {
            // If AAGen should not sort the Addressables groups in Addressables settings, then:
            if (!m_DataContainer.Settings.SortAddressableGroups)
            {
                // Do nothing else.
                return;
            }

            // Otherwise, AAGen should sort the Addressables groups in Addressables settings.

            // Order the groups in the Addressables settings by their group name. 
            AddressableSettings.groups.Sort(ComparisonLogic);
            
            int ComparisonLogic(AddressableAssetGroup a, AddressableAssetGroup b)
            {
                // Compare one group name with the next.
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            }
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
            if (!IsReportEnabled)
                return;
            
            var reporter = GetType();
            
            if (m_DataContainer.Settings.RemoveUnnecessaryEntries)
            {
                var summary = $"Unused Entries Removed ({m_RemovedEntriesReport.Count})";
                object data = m_RemovedEntriesReport;
                JsonReport.SaveJsonReport(reporter, reporter.Name + "_UnusedEntriesRemoved", summary, data);
            }
            
            if (m_DataContainer.Settings.RemoveEmptyGroups)
            {
                var summary = $"Empty Groups Removed ({m_RemovedGroupsReport.Count})";
                object data = m_RemovedGroupsReport;
                JsonReport.SaveJsonReport(reporter, reporter.Name + "_EmptyGroupsRemoved", summary, data);
            }
            
            if (m_DataContainer.Settings.RemoveAddressableScenesFromBuildProfile)
            {
                var summary = $"Scenes Removed from Build Profile ({m_AddressableScenes.Count})";
                object data = m_AddressableScenes;
                JsonReport.SaveJsonReport(reporter, reporter.Name + "_ScenesRemoved", summary, data);
            }
        }
        #endregion
    }
}
