namespace Scaffold.Tools.List
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Provides extension methods for generic List and IList collections.
    /// The main goal is to simplify operations like shuffling, splitting, and set comparison.
    /// </summary>
    /// <remarks>
    /// Extensively used for data manipulation and ensuring set logic consistency across collections.
    /// </remarks>
    public static class ListExtensions
    {
        /// <summary>
        /// Determines whether a given list is null, entirely empty, or contains only null elements.
        /// The main goal is to perform a deep emptiness check.
        /// </summary>
        /// <typeparam name="T">The generic type of elements.</typeparam>
        /// <param name="list">The list to inspect.</param>
        /// <returns>True if the list is effectively empty, false otherwise.</returns>
        /// <remarks>
        /// Handy for filtering out junk or default-initialized collections during data processing.
        /// </remarks>
        public static bool IsNullOrEmptyOrAllEmpty<T>(this List<T> list)
        {
            if (list == null || !list.Any())
            {
                return true;
            }
            return list.TrueForAll(x => x == null);
        }

        /// <summary>
        /// Randomizes the order of the elements within the list using the Fisher-Yates algorithm.
        /// The main goal is to scramble a collection in place.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="list">The mutable list to shuffle.</param>
        /// <remarks>
        /// Used during gameplay, like dealing hands or randomizing queue sequences.
        /// </remarks>
        public static void Shuffle<T>(this List<T> list)
        {
            Random random = new Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }
        }

        /// <summary>
        /// Chunks the original list into a collection of adjacent element pairs.
        /// The main goal is to subdivide a single list into manageable size-2 batches.
        /// </summary>
        /// <typeparam name="T">The element classification.</typeparam>
        /// <param name="originalList">The flat list mapping source.</param>
        /// <returns>A new list housing pairs of the original elements.</returns>
        /// <remarks>
        /// Employed in layout construction or matching logic requiring sequence divisions.
        /// </remarks>
        public static List<List<T>> SplitIntoListOfPairs<T>(this List<T> originalList)
        {
            List<List<T>> result = new List<List<T>>();

            for (int i = 0; i < originalList.Count; i += 2)
            {
                List<T> pair = originalList.Skip(i).Take(2).ToList();
                result.Add(pair);
            }
            return result;
        }

        /// <summary>
        /// Checks if two lists contain exactly the same elements. Order is ignored, but duplicate frequency matters.
        /// The main goal is to establish equality of collection contents logically.
        /// </summary>
        /// <typeparam name="T">Base array type.</typeparam>
        /// <param name="first">The first collection vector.</param>
        /// <param name="second">The secondary comparative instance.</param>
        /// <returns>True if they share identical contents.</returns>
        /// <remarks>
        /// Ideal when comparing states populated independently that represent the same state configuration.
        /// </remarks>
        public static bool HasSameElementsAs<T>(this IList<T> first, IList<T> second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            return first.Count == second.Count && !first.Except(second).Any() && !second.Except(first).Any();
        }

        /// <summary>
        /// Evaluates if two lists possess identical unique subsets, disregarding frequency and sequence entirely.
        /// The main goal is to verify if both contain the same overall distinct elements.
        /// </summary>
        /// <typeparam name="T">The element definition.</typeparam>
        /// <param name="first">First evaluated group.</param>
        /// <param name="second">Second evaluation match.</param>
        /// <returns>True if distinct boundaries line up perfectly.</returns>
        /// <remarks>
        /// Used to ensure players have the same unlocked keys or inventory categories logically mapping sets.
        /// </remarks>
        public static bool HasSameUniqueElementsAs<T>(this IList<T> first, IList<T> second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            return new HashSet<T>(first).SetEquals(second);
        }

        /// <summary>
        /// Gathers the intersection of items found natively in both mapped enumerables.
        /// The main goal is to discover common overlaps.
        /// </summary>
        /// <typeparam name="T">Type criteria parameter constraint.</typeparam>
        /// <param name="first">The initial mapped variable array.</param>
        /// <param name="second">The overlap cross-comparison target query set.</param>
        /// <returns>A subset enumerable hosting common matching elements shared amongst both.</returns>
        /// <remarks>
        /// Employed during filter reductions where results only proceed if they conform across multiple constraints.
        /// </remarks>
        public static IEnumerable<T> GetCommonElements<T>(this IEnumerable<T> first, IEnumerable<T> second)
        {
            if (first == null || second == null)
            {
                return Enumerable.Empty<T>();
            }

            return first.Intersect(second);
        }
    }
}
