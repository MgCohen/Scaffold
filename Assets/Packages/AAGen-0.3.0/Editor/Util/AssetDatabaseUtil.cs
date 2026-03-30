using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace AAGen
{
    public static class AssetDatabaseUtil
    {
        /// <summary>
        /// Load all the assets in the project that are of type <see cref="{T}"/> and compile them into a list. 
        /// </summary>
        /// <returns>A container of all the <see cref="{T}"/> in the project.</returns>
        public static List<T> FindAssetsOfType<T>() where T : UnityEngine.Object
        {
            // Find all the guids associated with the assets that are instanced as the type.
            var guids = FindAssetGuidsForType<T>();
            var assets = new List<T>(guids.Length);

            // For every asset guid, perform the following:
            foreach (string guid in guids)
            {
                // Get the file path associated with the asset.
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Attempt to deserialize an instance of the object from the file path.
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);

                // If the asset is valid, then:
                if (asset != null)
                {
                    // Add the asset to the list of results.
                    assets.Add(asset);
                }
            }

            // Return the list of assets of the type.
            return assets;
        }

        /// <summary>
        /// Finds all the file paths associated with the assets that are instanced as type <see cref="{T}"/>
        /// </summary>
        /// <typeparam name="T">The type that the assets are instanced as.</typeparam>
        /// <returns>A collection of asset file paths that are associated with type <see cref="{T}"/></returns>
        public static List<string> FindAssetPathsForType<T>() where T : UnityEngine.Object
        {
            // Find all the guids associated with the assets that are instanced as the type.
            var guids = FindAssetGuidsForType<T>();

            // For each guid associated with the asset, transform them to the file path associated with the asset.
            return guids.Select(AssetDatabase.GUIDToAssetPath).ToList();
        }

        /// <summary>
        /// Finds all the guids associated with the assets that are instanced as type <see cref="{T}"/>
        /// </summary>
        /// <typeparam name="T">The type that the assets are instanced as.</typeparam>
        /// <returns>A collection of asset guids that are associated with type <see cref="{T}"/></returns>
        public static string[] FindAssetGuidsForType<T>() where T : UnityEngine.Object
        {
            // Find all the guids associated with the assets that are instanced as the type.
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        }
    }
}