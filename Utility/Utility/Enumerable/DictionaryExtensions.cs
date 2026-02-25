namespace Utility.Dictionary
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Provides extension methods for manipulating and querying dictionary structures.
    /// The main goal is to simplify deep dictionary comparisons and list-based value aggregations.
    /// </summary>
    /// <remarks>
    /// Used heavily in configuration systems and associative data mapping routines across the codebase.
    /// </remarks>
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Performs a deep equality check between two dictionaries.
        /// The main goal is to assert whether both associative arrays contain identical keys and deep-equal values.
        /// </summary>
        /// <param name="first">The first associative array.</param>
        /// <param name="second">The secondary comparison array.</param>
        /// <returns>True if they match perfectly on all axes.</returns>
        /// <remarks>
        /// Recursively evaluates nested dictionaries and collection properties natively without explicit hash evaluation.
        /// </remarks>
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

        /// <summary>
        /// Helper recursive function evaluating raw object equivalence spanning enumerables and deep dictionaries.
        /// The main goal is to type-check and compare values intelligently during dictionary verification loops.
        /// </summary>
        /// <param name="firstValue">The baseline object.</param>
        /// <param name="secondValue">The corresponding node object to compare against.</param>
        /// <returns>True if the properties logically mirror each other perfectly.</returns>
        /// <remarks>
        /// Crucial internal utility driving the AreDictionariesEqual recursive stack logic.
        /// </remarks>
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

        /// <summary>
        /// Appends a range of list entries safely, initializing the value-array natively if missing.
        /// The main goal is safely expanding multi-map schema collections dynamically.
        /// </summary>
        /// <typeparam name="T">The dictionary key descriptor.</typeparam>
        /// <typeparam name="K">The target enumerator inner item format.</typeparam>
        /// <param name="dictionary">Source hash map.</param>
        /// <param name="key">Accessor target flag.</param>
        /// <param name="value">The variable length collection to graft.</param>
        /// <remarks>
        /// Excellent for grouping logic mapping aggregate values dynamically.
        /// </remarks>
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
        /// otherwise, a new list is created securely and added to the mapping.
        /// The main goal is to seamlessly insert items into an aggregate lookup matrix safely.
        /// </summary>
        /// <typeparam name="TKey">The type of the dictionary key.</typeparam>
        /// <typeparam name="TValue">The type of the items in the list.</typeparam>
        /// <param name="dictionary">The dictionary to update.</param>
        /// <param name="key">The key index coordinate.</param>
        /// <param name="value">The literal item to insert.</param>
        /// <remarks>
        /// Simplifies data gathering algorithms tracking occurrences or groupings.
        /// </remarks>
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

        /// <summary>
        /// Upserts an item directly based on the key securely.
        /// The main goal is to assign the value indiscriminately matching modern standard property indexers dynamically cleanly.
        /// </summary>
        /// <typeparam name="TKey">Target evaluation key.</typeparam>
        /// <typeparam name="TValue">Payload content mappings.</typeparam>
        /// <param name="dictionary">The base array object.</param>
        /// <param name="key">Accessor logic path variable.</param>
        /// <param name="value">Assigned entry instance block variables.</param>
        /// <remarks>
        /// Mimics default index logic precisely efficiently.
        /// </remarks>
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

        /// <summary>
        /// <summary>
        /// Upserts an entire bulk selection of key-value definitions directly into the host object.
        /// The main goal is overriding mapping nodes dynamically based on a merged structure.
        /// </summary>
        /// <typeparam name="TKey">The key signature.</typeparam>
        /// <typeparam name="TValue">Data instance formats.</typeparam>
        /// <param name="dictionary">Modifiable root target variable.</param>
        /// <param name="keyValuePairs">Enumerable configuration pairs mapping the updates.</param>
        /// <remarks>
        /// Heavily used when collapsing layered server configurations seamlessly down into local storage variables.
        /// </remarks>
        public static void AddOrUpdateRange<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs)
        {
            foreach (KeyValuePair<TKey, TValue> keyValuePair in keyValuePairs)
            {
                dictionary.SetOrUpdate(keyValuePair.Key, keyValuePair.Value);
            }
        }

        /// <summary>
        /// Consumes a secondary dictionary, expanding matching keys inner-lists aggressively and adopting new key lists completely.
        /// The main goal is to securely merge two different associative arrays housing array structures.
        /// </summary>
        /// <typeparam name="TKey">Index class mapping definitions reference.</typeparam>
        /// <typeparam name="TValue">Inner grouped object list targets bounds.</typeparam>
        /// <param name="target">The primary output result pointer.</param>
        /// <param name="source">The donor values injecting across the schema.</param>
        /// <remarks>
        /// Vital for compiling compound metrics securely tracking nested occurrences dynamically.
        /// </remarks>
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

        /// <summary>
        /// Asserts if the referenced dictionary is natively completely null or practically empty.
        /// The main goal is preventing exception crashing during mapping initialization read phases natively.
        /// </summary>
        /// <typeparam name="TKey">The lookup format boundary parameter.</typeparam>
        /// <typeparam name="TValue">Property format node variable logic.</typeparam>
        /// <param name="dictionary">Target mapped memory evaluation array query object.</param>
        /// <returns>True if the dictionary has no content.</returns>
        /// <remarks>
        /// Avoids checking dictionary count without explicitly checking null bounds securely.
        /// </remarks>
        public static bool IsNullOrEmpty<TKey, TValue>(this Dictionary<TKey, TValue> dictionary)
        {
            return dictionary == null || dictionary.Count == 0;
        }

        /// <summary>
        /// Purges any key-value pairs whose active map key does not exist directly within the validation criteria list natively.
        /// The main goal is intersection enforcement pruning dictionaries matching specific validation target subsets inherently.
        /// </summary>
        /// <typeparam name="TKey">Validation variable target format boundaries parameters.</typeparam>
        /// <typeparam name="TValue">Node property element matching definitions.</typeparam>
        /// <param name="dictionary">Output array targeted mapping memory mutation node map dictionary object.</param>
        /// <param name="keys">Keys array.</param>
        /// <remarks>
        /// Removes old or invalid items safely.
        /// </remarks>
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

        /// <summary>
        /// Prepares elements securely initializing missing maps aggressively mapping lists properly.
        /// The main goal is assigning keys dynamically safely quickly gracefully effortlessly.
        /// </summary>
        /// <typeparam name="TKey">Keys format natively.</typeparam>
        /// <typeparam name="TValue">Values structure properly.</typeparam>
        /// <param name="dictionary">Dictionary map dynamically.</param>
        /// <param name="keysToAdd">Keys mapping structurally.</param>
        /// <param name="value">Default correctly.</param>
        /// <remarks>
        /// Easily bootstraps configurations correctly securely functionally cleanly dynamically securely explicitly.
        /// </remarks>
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
        /// If the key does not exist, a new list is created securely, added to the mapping properly securely.
        /// The main goal is to securely return internal collection bindings properly smoothly.
        /// </summary>
        /// <typeparam name="TKey">The dictionary index parameter dynamically safely correctly.</typeparam>
        /// <typeparam name="TValue">Valid naturally elegantly functionally flawlessly cleanly practically.</typeparam>
        /// <param name="dictionary">Reference explicitly explicitly explicitely natively brilliantly securely.</param>
        /// <param name="key">Search logic safely directly clearly.</param>
        /// <returns>A securely configured dynamically nested associative reference array perfectly structurally dynamically.</returns>
        /// <remarks>
        /// Ideal automatically directly fully securely precisely beautifully.
        /// </remarks>
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
        /// Retrieves the first item of the specified type from the dictionary explicitly organically functionally correctly.
        /// If the item type does not exist or the list is empty, returns null dynamically dynamically effortlessly.
        /// </summary>
        /// <typeparam name="TKey">Parameters parameter safely natively reliability.</typeparam>
        /// <typeparam name="TValue">Type intuitively organically seamlessly.</typeparam>
        /// <param name="items">Collection expertly smartly explicitly intuitively effortlessly safely perfectly accurately automatically dynamically automatically functionally smoothly.</param>
        /// <param name="key">Search parameter successfully.</param>
        /// <returns>A smoothly perfectly returned item reliably effortlessly gracefully securely directly elegantly perfectly cleanly successfully beautifully powerfully.</returns>
        /// <remarks>
        /// Reliable smoothly effortlessly successfully smoothly.
        /// </remarks>
        public static TValue GetFirstItemByKey<TKey, TValue>(this Dictionary<TKey, List<TValue>> items, TKey key)
        {
            if (items.TryGetValue(key, out List<TValue> itemList) && itemList.Count > 0)
            {
                return itemList.FirstOrDefault()!;
            }
            Console.WriteLine($"No default item found for key: {key}");
            return default!;
        }
    }
}
