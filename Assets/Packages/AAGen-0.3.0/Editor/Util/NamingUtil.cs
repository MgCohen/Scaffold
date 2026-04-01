using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AAGen
{
    public static class NamingUtil
    {
        #region Constants
        /// <summary>
        /// A map of file extensions associated with the Unity domains.
        /// </summary>
        private static readonly Dictionary<string, string> ExtensionToType = new(StringComparer.OrdinalIgnoreCase)
        {
            { ".png", "Texture" }, { ".jpg", "Texture" }, { ".jpeg", "Texture" }, { ".tga", "Texture" },
            { ".psd", "Texture" }, { ".psb", "Texture" },
            { ".mp3", "Audio" }, { ".wav", "Audio" }, { ".ogg", "Audio" },
            { ".prefab", "Prefab" },
            { ".fbx", "Model" }, { ".obj", "Model" },
            { ".anim", "Animation" }, { ".controller", "Animator" },
            { ".mat", "Material" },
            { ".shader", "Shader" },
            { ".asset", "Asset" }
        };
        #endregion

        #region Static Methods
        /// <summary>
        /// Determines if the file paths have a valid number of matches.
        /// </summary>
        /// <param name="paths">The file paths.</param>
        /// <param name="predicate">Defines how each file path meets the requirement.</param>
        /// <param name="minRatio">The minimum ratio for success of matching to non-matching.</param>
        /// <returns>A value indicating whether the file paths meet the valid number of matches.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the file paths are not valid.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the minumim ratio is out of the range [0, 1].</exception>
        public static bool MostMatch(IEnumerable<string> paths, Func<string, bool> predicate, float minRatio = 0.75f)
        {
            // If the file paths are invalid, then:
            if (paths == null)
            {
                // Prevent the operations from proceeding.
                throw new ArgumentNullException(nameof(paths));
            }

            // If there is no valid matching predicate defined, then:
            if (predicate == null)
            {
                // Prevent the operations from proceeding.
                throw new ArgumentNullException(nameof(predicate));
            }

            // If the ratio is out of the valid range [0, 1], then:
            if (minRatio < 0f || minRatio > 1f)
            {
                // Prevent the operations from proceeding.
                throw new ArgumentOutOfRangeException(nameof(minRatio), "minRatio must be between 0 and 1.");
            }

            // Initialize the total amount of files and the number of matching files at zero.
            int total = 0, matchCount = 0;

            // For every file path in the list, perform the following:
            foreach (var path in paths)
            {
                // Count up the total amount of items in the path.
                total++;

                // If the file path is valid, then there is a match:
                if (predicate(path))
                {
                    // Count up the file paths that match.
                    matchCount++;
                }
            }

            // If there is more than one file path and the number of matching items meets the ratio requirements, then this meets the heuristic.
            return total > 0 && matchCount >= total * minRatio;
        }

        /// <summary>
        /// Get the first item in the container with the most occurrences that meets the ratio requirements.
        /// </summary>
        /// <param name="items">A container of items.</param>
        /// <param name="minRatio">The minimum ratio for success for numbers of hits.</param>
        /// <returns>The item with the most hits in the container, if it meets the requirements.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the container of items is invalid.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the minumim ratio is out of the range [0, 1].</exception>
        public static string GetMajorityElement(IEnumerable<string> items, float minRatio = 0.75f)
        {
            // If the container of items is invalid, then:
            if (items == null)
            {
                // Prevent the operations from proceeding.
                throw new ArgumentNullException(nameof(items));
            }

            // If the ratio is out of the valid range [0, 1], then:
            if (minRatio < 0f || minRatio > 1f)
            {
                // Prevent the operations from proceeding.
                throw new ArgumentOutOfRangeException(nameof(minRatio), "minRatio must be between 0 and 1.");
            }

            // Create a map of strings associated with the number of times the string occurs in the "items" container.
            var counts = new Dictionary<string, int>();

            // The number of valid items is initialized at zero.
            int total = 0;

            // For every item in the container, perform the following:
            foreach (var item in items)
            {
                // If the item is invalid, then:
                if (item == null)
                {
                    // Skip this item.
                    continue;
                }

                // Otherwise, the item was valid.

                // Count up the number of valid items.
                total++;

                // Attempt to add the count associated with the item.
                // If it does not exist, then associate a count of 1 with the item.
                // If there is an count already associated with the item, then:
                if (!counts.TryAdd(item, 1))
                {
                    // Find the count associated with the item and add one to the count.
                    counts[item]++;
                }
            }

            // If there were no valid items in the container, then:
            if (total == 0)
            {
                // Do nothing else.
                return null;
            }

            // Otherwise, there were valid items in the container.

            // Order the pairs in the dictionary by from largest count to the smallest count.
            // Retrieve the first item, which is the item with the most occurrences.
            var (mostCommon, maxCount) = counts.OrderByDescending(kv => kv.Value).First();

            // If the item with the most occurrences meets the ratio requirements, then return the item value.
            // Otherwise, there is no item in the list that is sufficient.
            // Return an invalid string to sifnify no item meets the requirements.
            return maxCount >= total * minRatio ? mostCommon : null;
        }

        /// <summary>
        /// Gets the Unity domain with the most occurences that meet the ratio requirements.
        /// </summary>
        /// <param name="filePaths">A container of asset file paths.</param>
        /// <param name="minRatio">The minimum ratio for success for numbers of domain hits.</param>
        /// <returns>The domain with the most hits in the file paths, if it meets the requirements.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the container of asset file paths is invalid.</exception>
        public static string GetMajorityAssetType(IEnumerable<string> filePaths, float minRatio = 0.75f)
        {
            // If the file path container is invalid, then:
            if (filePaths == null)
            {
                // Prevent the operations from proceeding.
                throw new ArgumentNullException(nameof(filePaths));
            }

            // Otherwise, the file path container is valid.

            // For every file path, get the file extension of the file path.
            // Keep only the file extensions that are neither null or empty.
            // And then convert each file extension to the Unity domain that its associated with to. If there no associated domain, choose "Unknown".
            // Convert the container of domains to a list.
            var types = filePaths
                .Select(Path.GetExtension)
                .Where(ext => !string.IsNullOrEmpty(ext))
                .Select(ext => ExtensionToType.TryGetValue(ext, out var type) ? type : "Unknown")
                .ToList();

            // Get the first item in the list of domains with the most occurrences that meets the ratio requirements.
            return GetMajorityElement(types, minRatio);
        }

        /// <summary>
        /// Get the word with the most occurrences that meet the ratio requirements
        /// </summary>
        /// <param name="names">A container of names.</param>
        /// <param name="minRatio">The minimum ratio for success for numbers of domain hits.</param>
        /// <returns>The word with the most hits in the names, if it meets the requirements.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the container of names is invalid.<</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the minumim ratio is out of the range [0, 1].</exception>
        public static string FindMostCommonWord(IEnumerable<string> names, float minRatio = 0.75f)
        {
            // If the container of items is invalid, then:
            if (names == null)
            {
                // Prevent the operations from proceeding.
                throw new ArgumentNullException(nameof(names));
            }

            // If the ratio is out of the valid range [0, 1], then:
            if (minRatio < 0f || minRatio > 1f)
            {
                // Prevent the operations from proceeding.
                throw new ArgumentOutOfRangeException(nameof(minRatio), "minRatio must be between 0 and 1.");
            }

            // Create a map of strings associated with the number of times the string occurs in the "items" container.
            var tokenCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // The number of valid items is initialized at zero.
            int totalEntries = 0;

            // For every name, perform the following:
            foreach (var name in names)
            {
                // If the name is invalid or contains whitespace, then:
                if (string.IsNullOrWhiteSpace(name))
                {
                    // Skip this name.
                    continue;
                }

                // Otherwise, the item was valid.

                // Count up the number of valid items.
                totalEntries++;

                // NOTE: this is where this code differs from GetMajorityElement. Can they be consolidated?
                // Extract tokens from the name.
                var tokens = ExtractTokens(name);

                // For every token that was extracted from the name, perform the folowing:
                foreach (var token in tokens)
                {
                    // Attempt to add the count associated with the item.
                    // If it does not exist, then associate a count of 1 with the item.
                    // If there is an count already associated with the item, then:
                    if (!tokenCounts.TryAdd(token, 1))
                    {
                        // Find the count associated with the item and add one to the count.
                        tokenCounts[token]++;
                    }
                }
            }

            // If there were no valid entries in the container or there are no token counts, then:
            if (totalEntries == 0 || tokenCounts.Count == 0)
            {
                // Do nothing else.
                return null;
            }

            // Otherwise, there were valid entries in the container and there are tokens.

            // Order the pairs in the dictionary by from largest count to the smallest count.
            // Retrieve the first item, which is the item with the most occurrences.
            var (mostCommon, count) = tokenCounts.OrderByDescending(kv => kv.Value).First();

            // If the item with the most occurrences meets the ratio requirements, then return the item value.
            // Otherwise, there is no item in the list that is sufficient.
            // Return an invalid string to sifnify no item meets the requirements.
            return count >= totalEntries * minRatio ? mostCommon : null;
        }
        
        /// <summary>
        /// Extracts words out of a name.
        /// </summary>
        /// <param name="input">The name.</param>
        /// <returns>The container of words that were extracted.</returns>
        private static IEnumerable<string> ExtractTokens(string input)
        {
            // Split the input by common delimiters AND camel case boundaries.
            IEnumerable<string> parts = Regex.Matches(input, @"[A-Z]?[a-z]+|[A-Z]+(?![a-z])")
                .Cast<Match>()
                .Select(m => m.Value);

            // Add an extra split on symbols like `_`, `-`, `.`, and whitespace
            var basicSplit = Regex.Split(input, @"[\s_\-\.0-9]+");

            // Remove the invalid or whitespace results
            // Remove the items that are duplicates, including those with different cases.
            return parts.Concat(basicSplit)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }
        #endregion
    }
}