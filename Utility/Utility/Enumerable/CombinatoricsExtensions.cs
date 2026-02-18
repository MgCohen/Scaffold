namespace Utility.Combinatorics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class CombinatoricsArrayExtensions
    {
        /// <summary>
        /// STREAMING: yields all non-empty combinations as T[] (power set minus empty).
        /// Input is deduped first (optional comparer). Use maxSize to cap combination size.
        /// </summary>
        public static IEnumerable<T[]> AllCombinationsArrays<T>(
            this IEnumerable<T> source,
            int? maxSize = null,
            IEqualityComparer<T> comparer = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

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
        /// Materialized jagged array version (calls the streaming variant and ToArray()).
        /// </summary>
        public static T[][] AllCombinationsToJaggedArray<T>(
            this IEnumerable<T> source,
            int? maxSize = null,
            IEqualityComparer<T> comparer = null)
            => source.AllCombinationsArrays(maxSize, comparer).ToArray();

        // Recursive generator for exact-size k-combinations.
        // buffer[pos] holds the next picked element; when pos==k we emit a copy of length k.
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