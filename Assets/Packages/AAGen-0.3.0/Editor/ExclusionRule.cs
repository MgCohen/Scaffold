using UnityEngine;

namespace AAGen
{
    /// <summary>
    /// Represents the rules that are used to exclude assets from being input in the AAGen pipeline.
    /// </summary>
    public abstract class ExclusionRule : ScriptableObject
    {
        #region Fields
        /// <summary>
        /// A reference to the <see cref="DataContainer"/>.
        /// </summary>
        protected DataContainer m_DataContainer;
        #endregion

        #region Methods
        /// <summary>
        /// Initializes the input rule.
        /// </summary>
        /// <param name="dataContainer">A reference to the <see cref="DataContainer"/>.</param>
        public virtual void Initialize(DataContainer dataContainer)
        {
            m_DataContainer = dataContainer;
        }

        /// <summary>
        /// Determines whether a node should be ignored.
        /// </summary>
        /// <param name="node">The node in question.</param>
        /// <returns>A value indicating whether the node should be ignored.</returns>
        public abstract bool ShouldIgnoreNode(AssetNode node);

        /// <summary>
        /// Uninitializes the input rule.
        /// </summary>
        public virtual void UnInitialize()
        {
            m_DataContainer = null;
        }
        #endregion
    }
}
