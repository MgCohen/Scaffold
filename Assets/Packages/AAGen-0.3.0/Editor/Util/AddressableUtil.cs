using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace AAGen
{
    public static class AddressableUtil
    {
        #region Static Methods
        /// <summary>
        /// Finds the default Addressables Group Template.
        /// </summary>
        /// <returns>The default Addressables Group template.</returns>
        public static AddressableAssetGroupTemplate FindDefaultAddressableGroupTemplate()
        {
            // Get a reference to the Addressables Settings instance, which is used to configure Addressables runtime. 
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

            // If there is no valid settings instance, then Addressables has not been initialized in the project.
            // If Addressables has not been initialized in the project, then:
            if (settings == null)
            {
                // Throw an error that describes why the user cannot proceed.
                throw new("AddressableAssetSettings not found. Ensure Addressables are initialized.");
            }

            // Otherwise, Addressables has been initialized in the project.

            // Get the list of Addressables group templates which are used to instance Addressables groups from.
            List<ScriptableObject> templates = settings.GroupTemplateObjects;

            // If the group templates are invalid or there are none, then:
            if (templates == null || templates.Count == 0)
            {
                // Throw an error that prevents this from proceeding.
                throw new("No group templates found in Addressable Settings.");
            }

            // Otherwise, the group templates are valid and there is at least one.

            // Assume the first template is the default one.
            var defaultTemplate = templates[0];

            // Ensure that the instance is cast to the concrete type.
            return defaultTemplate as AddressableAssetGroupTemplate;
        }

        /// <summary>
        /// Gets a collection of all writable addressable entries.
        /// </summary>
        /// <returns>A collection of all writable addressable entries.</returns>
        public static List<AddressableAssetEntry> GetAddressableEntries()
        {
            var entries = new List<AddressableAssetEntry>();
            
            // Get a reference to the Addressables Settings instance, which is used to configure Addressables runtime. 
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            
            // If there is no valid settings instance, then Addressables has not been initialized in the project.
            // If Addressables has not been initialized in the project, then:
            if (settings == null)
            {
                // Return an empty list of Addressables entries.
                return entries;
            }
            
            // Otherwise, Addressables has been initialized in the project.
            
            // For every Addressables group that is not a read-only group, perform the following:
            foreach (var group in settings.groups.Where(group => !group.ReadOnly))
            {
                if (group == null) //in case of invalid groups
                    continue;
                
                // Add the writable group to the list of entries.
                entries.AddRange(group.entries);
            }
            
            return entries;
        }
        
        /// <summary>
        /// Returns a list of asset paths including addressable entries and
        /// assets inside in folder addressable entries
        /// </summary>
        /// <param name="includeFolderEntries"></param>
        /// <returns></returns>
        public static HashSet<string> GetExtendedAddressableEntries(bool includeFolderEntries = false)
        {
            var result = new HashSet<string>();

            foreach (var entry in GetAddressableEntries())
            {
                if (entry == null)
                    continue;

                string entryAssetPath = entry.AssetPath;
                if (string.IsNullOrEmpty(entryAssetPath))
                    continue;

                bool isFolder = AssetDatabase.IsValidFolder(entryAssetPath);

                if (isFolder)
                {
                    if (includeFolderEntries)
                        result.Add(entryAssetPath);

                    // Expand all assets under this folder (recursive)
                    var guidsUnderFolder = AssetDatabase.FindAssets(string.Empty, new[] { entryAssetPath });
                    foreach (var guid in guidsUnderFolder)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                        if (string.IsNullOrEmpty(assetPath))
                            continue;

                        if (AssetDatabase.IsValidFolder(assetPath))
                            continue; // skip subfolders

                        result.Add(assetPath);
                    }
                }
                else
                {
                    result.Add(entryAssetPath);
                }
            }

            return result;
        }
        #endregion
    }
}
