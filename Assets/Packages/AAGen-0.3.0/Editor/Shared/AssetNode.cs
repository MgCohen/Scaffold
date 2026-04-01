using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;

namespace AAGen
{
    /// <summary>
    /// Represents a graph node containing asset data.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="IEquatable{T}"/> to ensure compatibility with dictionary processing methods in the <see cref="Graph{T}"/> class.
    /// </remarks>
    [Serializable]
    public class AssetNode : IEquatable<AssetNode>
    {
        #region Static Methods
        /// <summary>
        /// Create a node using an asset path.
        /// </summary>
        /// <param name="assetPath">The asset path.</param>
        /// <returns>The node that was created. If the path is not has no valid asset associated, then the node returned is invalid.</returns>
        public static AssetNode FromAssetPath(string assetPath)
        {
            // If the asset path is invalid or empty, then:
            if (string.IsNullOrEmpty(assetPath))
            {
                // Return an invalid node. Do nothing else.
                return null;
            }

            // Otherwise, the asset path is valid and not empty.

            // Get the GUID that is associated with the asset path for assets that have already been imported and processed.
            var guidString = AssetDatabase.AssetPathToGUID(assetPath, AssetPathToGUIDOptions.OnlyExistingAssets);

            // Create a node associated with the asset GUID.
            return FromGuidString(guidString);
        }

        /// <summary>
        /// Create a node using the formatted string.
        /// </summary>
        /// <param name="value">The formatted string. Assumes the asset GUID is the first part of the string.</param>
        /// <returns>The node that was created. If the string was invalid, then the node returned is invalid.</returns>
        public static AssetNode FromString(string value)
        {
            // Split the string into an array od smaller strings using the delimiter as the item separating them.
            string[] parts = value.Split('|');

            // Get the first item in the array, which is assumed to be the asset GUID.
            var guidString = parts[0];

            // Create a node associated with the asset GUID.
            return FromGuidString(guidString);
        }

        /// <summary>
        /// Create a node using an asset GUID.
        /// </summary>
        /// <param name="guidString">The string version of the asset GUID.</param>
        /// <returns>The node that was created. If the GUID was invalid, then the node returned is invalid.</returns>
        public static AssetNode FromGuidString(string guidString)
        {
            // Attempt to parse the guid string representation into an actual data structure.
            // If the parse was successful, then create a new node with the GUID as its value.
            // Otherwise, the parse was unsuccessful. Return an invalid node.
            return GUID.TryParse(guidString, out var guid) ? new AssetNode(guid) : null;
        }
        #endregion

        #region Fields
        /// <summary>
        /// The GUID for the asset.
        /// </summary>
        [JsonIgnore]
        public readonly GUID Guid;
        #endregion

        #region Properties
        /// <summary>
        /// Get the file path of the asset.
        /// </summary>
        /// <remarks>
        /// This method uses the current state of the asset database to return a path for
        /// an entry in the dependency graph. It is only valid if the dependency graph is up to date.
        /// </remarks>
        public string AssetPath => AssetDatabase.GUIDToAssetPath(Guid);

        /// <summary>
        /// Get the file name of the asset.
        /// </summary>
        [JsonIgnore]
        public string FileName => Path.GetFileName(AssetPath);
        #endregion

        #region Methods
        /// <summary>
        /// Create a new instance of the <see cref="AssetNode"/> class.
        /// </summary>
        /// <param name="guid">The guid the asset is associated with in the Unity asset database.</param>
        /// <remarks>
        /// There is no default constructor since it has no meaning to these nodes without an asset guid.
        /// </remarks>
        public AssetNode(GUID guid) 
        {
            Guid = guid;
        }

        /// <summary>
        /// A string that represents the current object.
        /// </summary>
        /// <returns>Returns a string that represents the current object.</returns>
        public override string ToString()
        {
            return Guid.ToString(); 
        }

        /// <summary>
        /// Determines whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">A node to compare with this node.</param>
        /// <returns><see cref="true"/> if the object is equal to the current object; otherwise, <see cref="false"/>.</returns>
        public bool Equals(AssetNode other)
        {
            return other != null && Guid.Equals(other.Guid);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns><see cref="true"/> if the object is equal to the current object; otherwise, <see cref="false"/>.</returns>
        public override bool Equals(object obj)
        {
            return obj is AssetNode other && Equals(other);
        }

        /// <summary>
        /// Determines the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            return Guid.GetHashCode();
        }
        #endregion
    }
}
