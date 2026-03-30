using UnityEngine;

namespace AAGen
{
    /// <summary>
    /// Represents the default rules that are used to exclude or transform Group layouts as output in the AAGen pipeline.
    /// </summary>
    [CreateAssetMenu(menuName = Constants.ContextMenus.OutputRulesMenu + nameof(DefaultOutputRule))]
    public class DefaultOutputRule : OutputRule
    {
        #region Methods
        /// <summary>
        /// Determines whether a group layout matches the criteria for selection.
        /// </summary>
        /// <param name="groupLayout">The group layout.</param>
        /// <returns>A value indicating that the group layout should be included in the selection.</returns>
        protected override bool DoesMatchSelectionCriteria(GroupLayout groupLayout)
        {
            // All group layouts match the selection criteria.
            return true;
        }
        #endregion
    }
}