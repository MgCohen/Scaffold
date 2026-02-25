namespace Utility.Combinatorics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Provides extension methods for generating combinations (power sets) from generic enumerables.
    /// The main goal is to efficiently compute mathematical combinations iteratively.
    /// </summary>
    /// <remarks>
    /// Used when generating permutations and possible groupings of items during matchmaking, search generation, or mathematical analysis in the codebase.
    /// </remarks>
    public static class CombinatoricsArrayExtensions
    {
        /// <summary>
        /// Generates all non-empty combinations iteratively, streaming output as generic arrays (power set minus empty).
        /// Input is deduplicated first based on an optional comparer; limits combinations with maxSize.
        /// The main goal is to iteratively stream out combinatorics without holding them in-memory completely.
        /// </summary>
        /// <typeparam name="T">Type of elements to combine.</typeparam>
        /// <param name="source">The input data source.</param>
        /// <param name="maxSize">The cap configuration indicating maximum size of the grouping to generate.</param>
        /// <param name="comparer">Optional duplication evaluation equality comparer logic target.</param>
        /// <returns>A stream of different target combination arrays.</returns>
        /// <remarks>
        /// Very important for large item sets where generating all subsets normally causes Out Of Memory crashes.
        /// </remarks>
        public static IEnumerable<T[]> AllCombinationsArrays<T>(
            this IEnumerable<T> source,
            int? maxSize = null,
            IEqualityComparer<T> comparer = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            // Materialize once for indexing & to avoid re-enumerating a lazy source
            T[] items = (comparer == null ? source.Distinct() : source.Distinct(comparer)).ToArray();
            int n = items.Length;
            if (n == 0)
            {
                yield break;
            }

            int limit = maxSize.HasValue ? Math.Clamp(maxSize.Value, 1, n) : n;

            // Use a single buffer array to track the current combination
            T[] buffer = new T[limit];
            for (int k = 1; k <= limit; k++)
            {
                foreach (T[]? combo in KCombinationsArrays(items, k, 0, 0, buffer))
                {
                    yield return combo;
                }
            }
        }

        /// <summary>
        /// Generates all combination subsets as a completely materialized jagged array.
        /// The main goal is to synchronously evaluate combinations rather than streaming.
        /// </summary>
        /// <typeparam name="T">Element classification.</typeparam>
        /// <param name="source">Original starting parameter values.</param>
        /// <param name="maxSize">Count limiter.</param>
        /// <param name="comparer">Deduplication algorithm matching pattern reference instance.</param>
        /// <returns>A jagged array of all combinations.</returns>
        /// <remarks>
        /// Can take much heavier RAM space than the stream version. Used when all data MUST be strictly available.
        /// </remarks>
        public static T[][] AllCombinationsToJaggedArray<T>(
            this IEnumerable<T> source,
            int? maxSize = null,
            IEqualityComparer<T> comparer = null)
        {
            return source.AllCombinationsArrays(maxSize, comparer).ToArray();
        }

        /// <summary>
        /// A recursive generator for computing combinations mapping precise lengths of target sets against memory arrays functionally safely.
        /// The main goal is to track positional combinations down an element chain without excessive array duplication.
        /// </summary>
        /// <typeparam name="T">Base target matching definitions.</typeparam>
        /// <param name="items">Reference evaluation sets.</param>
        /// <param name="k">Target grouping size goal limit variable.</param>
        /// <param name="startIndex">Memory positional start.</param>
        /// <param name="pos">Recursive variable pointing reference definition value tracking positions dynamically currently processing iterations.</param>
        /// <param name="buffer">The core storage vector carrying element mappings dynamically.</param>
        /// <returns>Streams resulting combinations.</returns>
        /// <remarks>
        /// Recursively generates standard elements until pos == k, building mathematical combination logic matrices safely.
        /// </remarks>
        private static IEnumerable<T[]> KCombinationsArrays<T>(
            T[] items, int k, int startIndex, int pos, T[] buffer)
        {
            if (pos == k)
            {
                T[] outArr = new T[k];
                Array.Copy(buffer, 0, outArr, 0, k);
                yield return outArr;
                yield break;
            }

            int n = items.Length;
            for (int i = startIndex; i <= n - (k - pos); i++)
            {
                buffer[pos] = items[i];
                foreach (T[]? combo in KCombinationsArrays(items, k, i + 1, pos + 1, buffer))
                {
                    yield return combo;
                }
            }
        }
    }
}