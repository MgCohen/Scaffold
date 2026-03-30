using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AAGen
{
    /// <summary>
    /// Represents the rules that transfotm the names of group layouts to be more descriptive.
    /// </summary>
    [CreateAssetMenu(menuName = Constants.ContextMenus.OutputRulesMenu + nameof(ImprovedNamesOutputRule))]
    public class ImprovedNamesOutputRule : OutputRule
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

        /// <summary>
        /// Refine the selected group layouts.
        /// </summary>
        public override void Refine()
        {
            // Cache the unique set of asset file paths that are included as inputs to the AAGen pipeline.
            HashSet<string> inputAssets = m_DataContainer.InputAssets;

            // For each selected group layout, perform the following:
            foreach (var groupLayout in m_Selection)
            {
                // Cache the current group layout name, which is hash-based.
                var currentName = groupLayout.Name;
                
                // Includes only one input asset

                // For each node in the group layout, get the file path to the asset and pack into a set of unique file paths.
                var groupLayoutAssets = groupLayout.Nodes.Select(node => node.AssetPath).ToHashSet();

                // Create a unique set of assets that are input assets minus the assets in the group layout.
                var intersection = new HashSet<string>(inputAssets);
                intersection.IntersectWith(groupLayoutAssets);

                // If the intersection has at least one input asset that is not in the group layout, then:
                if (intersection.Count == 1)
                {
                    // Rename the group layout.
                    Rename(groupLayout, $"Hierarchy of {Path.GetFileName(intersection.ToList()[0])}_{currentName}");

                    // Skip this group layout and move onto the next.
                    continue;
                }

                // Otherwise, the intersection has none or multiple input assets that are not in the group layout

                // If the group layout has only one source, then:
                if (groupLayout.Sources.Count == 1)
                {
                    // Rename the group layout.
                    Rename(groupLayout, $"Dependencies of {groupLayout.Sources.ToList()[0].FileName}_{currentName}");

                    // Skip this group layout and move onto the next.
                    continue;
                }

                // Otherwise, the group layout has no sources or multiple.

                // If the group layout has only one node, then:
                if (groupLayout.Nodes.Count == 1)
                {
                    // Rename the group layout.
                    Rename(groupLayout, $"{groupLayout.Nodes.ToList()[0].FileName}_{currentName}");

                    // Skip this group layout and move onto the next.
                    continue;
                }

                // Otherwise, the group layout has no nodes or multiple.

                // If the group layout is shared by other layouts as a dependency, then:
                if (groupLayout.IsShared)
                {
                    // Get the current name and use it to format a new name
                    // that signifies that the group layout is shared.
                    var newName = $"Shared_{currentName}";

                    // Rename the group layout.
                    Rename(groupLayout, newName);

                    // Skip this group layout and move onto the next.
                    continue;
                }
            }
        }
        #endregion
    }
}