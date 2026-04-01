using System.Collections.Generic;
using UnityEngine;

namespace AAGen
{
    /// <summary>
    /// Represents the rules that are used to include assets as input in the AAGen pipeline.
    /// </summary>
    public abstract class InputRule : ScriptableObject
    {
        #region Methods
        /// <summary>
        /// Get a unique set of file paths for asests that should be included as inputs for AAGen.
        /// </summary>
        /// <returns>A unique set of file paths for asests that should be included as inputs for AAGen.</returns>
        public abstract HashSet<string> GetIncludedAssets();
        #endregion
    }
}