namespace Utility.Dictionary
{
    public static class DictionaryExtensions
    {
        public static bool AreDictionariesEqual(Dictionary<string, object> first, Dictionary<string, object> second)
        {
            // Check for null
            if (first == null && second == null)
            {
                return true;
            }

            if (first == null || second == null)
            {
                return false;
            }

            // Check count
            if (first.Count != second.Count)
            {
                return false;
            }

            // Compare each key and value
            foreach (KeyValuePair<string, object> pair in first)
            {
                if (!second.TryGetValue(pair.Key, out object secondValue))
                {
                    // Key does not exist in the second dictionary
                    return false;
                }

                if (!AreValuesEqual(pair.Value, secondValue))
                {
                    // Values are not equal
                    return false;
                }
            }

            return true;
        }

        private static bool AreValuesEqual(object firstValue, object secondValue)
        {
            // Check for null
            if (firstValue == null && secondValue == null)
            {
                return true;
            }

            if (firstValue == null || secondValue == null)
            {
                return false;
            }

            // Handle dictionary comparison recursively if values are dictionaries
            if (firstValue is Dictionary<string, object> firstDict && secondValue is Dictionary<string, object> secondDict)
            {
                return AreDictionariesEqual(firstDict, secondDict);
            }

            // Handle collections (e.g., lists, arrays)
            if (firstValue is IEnumerable<object> firstCollection && secondValue is IEnumerable<object> secondCollection)
            {
                IEnumerator<object> firstEnumerator = firstCollection.GetEnumerator();
                IEnumerator<object> secondEnumerator = secondCollection.GetEnumerator();

                while (firstEnumerator.MoveNext())
                {
                    if (!secondEnumerator.MoveNext() || !AreValuesEqual(firstEnumerator.Current, secondEnumerator.Current))
                    {
                        return false;
                    }
                }

                // Ensure no extra elements in the second collection
                return !secondEnumerator.MoveNext();
            }

            // Default comparison for primitive types and other objects
            return firstValue.Equals(secondValue);
        }
        
        public static void AddRangeToList<T, K>(this Dictionary<T, List<K>> dictionary, T key, List<K> value)
        {
            if (!dictionary.ContainsKey(key))
            {
                dictionary[key] = new List<K>();
            }
            dictionary[key].AddRange(value);
        }

        /// <summary>
        /// Adds an item to the dictionary. If the key exists, the item is added to the existing list;
        /// otherwise, a new list is created and added to the dictionary.
        /// </summary>
        /// <typeparam name="TKey">The type of the dictionary key.</typeparam>
        /// <typeparam name="TValue">The type of the items in the list.</typeparam>
        /// <param name="dictionary">The dictionary to update.</param>
        /// <param name="key">The key for the dictionary.</param>
        /// <param name="value">The item to add to the list.</param>
        public static void AddToList<TKey, TValue>(this Dictionary<TKey, List<TValue>> dictionary, TKey key, TValue value)
        {
            if (dictionary.TryGetValue(key, out List<TValue> itemList))
            {
                itemList.Add(value);
            }
            else
            {
                dictionary[key] = new List<TValue> { value };
            }
        }

        public static void SetOrUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary.ContainsKey(key))
            {
                dictionary[key] = value;
            }
            else
            {
                dictionary.Add(key, value);
            }
        }

        public static void AddOrUpdateRange<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs)
        {
            foreach (KeyValuePair<TKey, TValue> keyValuePair in keyValuePairs)
            {
                dictionary.SetOrUpdate(keyValuePair.Key, keyValuePair.Value);
            }
        }
        
        public static void MergeItems<TKey, TValue>(this Dictionary<TKey, List<TValue>> target, Dictionary<TKey, List<TValue>> source)
        {
            foreach (KeyValuePair<TKey, List<TValue>> entry in source)
            {
                if (target.ContainsKey(entry.Key))
                {
                    // If the key exists, add items to the existing list
                    target[entry.Key].AddRange(entry.Value);
                }
                else
                {
                    // If the key does not exist, create a new entry
                    target[entry.Key] = new List<TValue>(entry.Value);
                }
            }
        }

        public static bool IsNullOrEmpty<TKey, TValue>(this Dictionary<TKey, TValue> dictionary)
        {
            return dictionary == null || dictionary.Count == 0;
        }

        public static void RemoveKeysNotContainedInDictionary<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, List<TKey> keys)
        {
            List<TKey> keysToRemove = new List<TKey>();
            // Iterate through all the keys in the dictionary
            foreach (KeyValuePair<TKey, TValue> kvp in dictionary)
            {
                TKey key = kvp.Key;
                // Check if the key is not contained in the dictionary
                if (!keys.Contains(key))
                {
                    // Add the key to the list of keys to remove
                    keysToRemove.Add(key);
                }
            }

            // Remove the keys that were not found in the dictionary
            foreach (TKey keyToRemove in keysToRemove)
            {
                dictionary.Remove(keyToRemove);
            }
        }

        public static void InitializeDictionaryByKeyList<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, List<TKey> keysToAdd, TValue value)
        {
            // Add the keys that were not found in the dictionary
            foreach (TKey keyToAdd in keysToAdd)
            {
                dictionary.TryAdd(keyToAdd, value);
            }
        }
        
        /// <summary>
        /// Retrieves the list associated with the specified key in the dictionary.
        /// If the key does not exist, a new list is created, added to the dictionary, and returned.
        /// </summary>
        /// <typeparam name="TKey">The type of the dictionary key.</typeparam>
        /// <typeparam name="TValue">The type of the items in the list.</typeparam>
        /// <param name="dictionary">The dictionary to check and update.</param>
        /// <param name="key">The key to look for in the dictionary.</param>
        /// <returns>The existing or newly created list.</returns>
        public static List<TValue> GetOrCreateList<TKey, TValue>(this Dictionary<TKey, List<TValue>> dictionary, TKey key)
        {
            if (dictionary.TryGetValue(key, out List<TValue> itemList))
            {
                return itemList;
            }

            List<TValue> newItems = new();
            dictionary.Add(key, newItems);
            return newItems;
        }
        
        /// <summary>
        /// Retrieves the first item of the specified type from the dictionary.
        /// If the item type does not exist or the list is empty, returns null.
        /// </summary>
        /// <typeparam name="TValue">The type of the items in the list.</typeparam>
        /// <param name="items">The dictionary containing lists of items categorized by type.</param>
        /// <param name="TKey">The key representing the item type.</param>
        /// <returns>The first item of the specified type, or null if not found.</returns>
        public static TValue GetFirstItemByKey<TKey, TValue>(this Dictionary<TKey, List<TValue>> items, TKey key)
        {
            if (items.TryGetValue(key, out List<TValue> itemList) && itemList.Count > 0 )
            {
                return itemList.FirstOrDefault();
            }
            Console.WriteLine($"No default item found for key: {key}");
            return default;
        }
    }
}