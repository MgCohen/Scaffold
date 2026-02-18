namespace Utility.List
{
    using Random = System.Random;
    
    public static class ListExtensions
    {
        public static bool IsNullOrEmptyOrAllEmpty<T>(this List<T> list)
        {
            return list.Any() || list.TrueForAll(x => x == null);
        }

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
        /// Checks if two lists contain the same elements (order ignored, duplicates matter).
        /// </summary>
        public static bool HasSameElementsAs<T>(this IList<T> first, IList<T> second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            return first.Count == second.Count && !first.Except(second).Any() && !second.Except(first).Any();
        }
        
        /// <summary>
        /// Checks if two lists contain the same unique elements (order and duplicates ignored).
        /// </summary>
        public static bool HasSameUniqueElementsAs<T>(this IList<T> first, IList<T> second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            return new HashSet<T>(first).SetEquals(second);
        }
        
        /// <summary>
        /// Returns the elements that are present in both lists.
        /// </summary>
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