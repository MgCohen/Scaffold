using UnityEngine;

namespace Scaffold.Entities
{
    /// <summary>
    /// Identifies a float-backed stat or flag on an <see cref="Entity"/>; behaviors reference this asset instead of string literals.
    /// The logical id is the Unity asset name (filename without extension), exposed via <see cref="AttributeName"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Scaffold/Entity/Entity Attribute", fileName = "EntityAttribute")]
    public class EntityAttribute : ScriptableObject
    {
        /// <summary>
        /// Same as <see cref="Object.name"/>: for assets on disk, rename the asset file to change this id.
        /// </summary>
        public string AttributeName => name;
    }
}
