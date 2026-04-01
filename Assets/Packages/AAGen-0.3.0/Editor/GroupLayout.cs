using System;
using Newtonsoft.Json;

namespace AAGen
{
    /// <summary>
    /// Represents a way to define how groups are laid out.
    /// </summary>
    [Serializable]
    public class GroupLayout : Subgraph
    {
        #region Fields
        /// <summary>
        /// The name of the Addressables Group Template to use.
        /// </summary>
        public string TemplateName;

        /// <summary>
        /// The name of the group layout.
        /// </summary>
        public string Name;
        #endregion

        #region Properties
        /// <summary>
        /// Gets a value indicating whether the group layout is shared by other group layouts as a dependency.
        /// </summary>
        [JsonIgnore]
        public bool IsShared => Sources.Count > 1;
        #endregion
    }
}