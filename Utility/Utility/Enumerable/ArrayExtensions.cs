using System.Collections.Generic;
using System.Linq;

namespace Utility.Array
{
    /// <summary>
    /// Provides extension methods for common array and enumerable operations.
    /// The main goal is to ease functional array mutations and aggregate logic evaluations.
    /// </summary>
    /// <remarks>
    /// Used when working directly with arrays instead of dynamically resizing lists, and checking conditions across boolean sets.
    /// </remarks>
    public static class ArrayExtensions
    {
        /// <summary>
        /// Creates a new array and inserts the provided element at its beginning.
        /// The main goal is to functionally prepend an item without mutating the original.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="array">The source array.</param>
        /// <param name="newElement">The element to insert.</param>
        /// <returns>A new array with the element prepended.</returns>
        /// <remarks>
        /// Used for immutability-focused operations that need to shift new properties to start of a sequence.
        /// </remarks>
        public static T[] InsertAtBeginning<T>(this T[] array, T newElement)
        {
            T[] newArray = new T[array.Length + 1];
            newArray[0] = newElement;
            for (int i = 0; i < array.Length; i++)
            {
                newArray[i + 1] = array[i];
            }
            return newArray;
        }

        /// <summary>
        /// Creates a new array with the target item added to the end.
        /// The main goal is to functionally append one element safely.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="originalArray">The source array.</param>
        /// <param name="itemToAdd">The target element.</param>
        /// <returns>A larger array with the added element.</returns>
        /// <remarks>
        /// Useful for appending logic outside of heavy collection abstractions like List where fixed lengths are required.
        /// </remarks>
        public static T[] AppendToArray<T>(this T[] originalArray, T itemToAdd)
        {
            T[] newArray = new T[originalArray.Length + 1];
            for (int i = 0; i < originalArray.Length; i++)
            {
                newArray[i] = originalArray[i];
            }
            newArray[newArray.Length - 1] = itemToAdd;
            return newArray;
        }

        /// <summary>
        /// Appends a collection of items to the original array by generating a new combined array.
        /// The main goal is to concatenate a sequence natively.
        /// </summary>
        /// <typeparam name="T">The type of items.</typeparam>
        /// <param name="originalArray">The source array array.</param>
        /// <param name="itemsToAdd">The enumerable of elements adding on.</param>
        /// <returns>A newly constructed array comprising both sequences.</returns>
        /// <remarks>
        /// Used to mass-expand arrays efficiently via underlying List range operations.
        /// </remarks>
        public static T[] AppendToArray<T>(this T[] originalArray, IEnumerable<T> itemsToAdd)
        {
            List<T> newList = new List<T>(originalArray);
            newList.AddRange(itemsToAdd);
            return newList.ToArray();
        }

        /// <summary>
        /// Removes the first occurrence of a specified item from the original array.
        /// The main goal is to functionally deduct an element and return a smaller array.
        /// </summary>
        /// <typeparam name="T">The type of the element.</typeparam>
        /// <param name="originalArray">The source array.</param>
        /// <param name="itemToRemove">The item matched for removal.</param>
        /// <returns>A new array omitting the removed item.</returns>
        /// <remarks>
        /// Helpful when maintaining pure function designs over generic collections.
        /// </remarks>
        public static T[] RemoveFromArray<T>(this T[] originalArray, T itemToRemove)
        {
            List<T> newList = new List<T>(originalArray);
            newList.Remove(itemToRemove);
            return newList.ToArray();
        }

        /// <summary>
        /// Iteratively drops numerous elements from the source array.
        /// The main goal is to functionally cull large sets of items safely.
        /// </summary>
        /// <typeparam name="T">Item type logic.</typeparam>
        /// <param name="originalArray">Source base array.</param>
        /// <param name="itemsToRemove">The collection of matches to purge.</param>
        /// <returns>A compacted version of the original source.</returns>
        /// <remarks>
        /// Used for batch processing and clearing of defunct entities.
        /// </remarks>
        public static T[] RemoveFromArray<T>(this T[] originalArray, IEnumerable<T> itemsToRemove)
        {
            List<T> newList = new List<T>(originalArray);
            foreach (T item in itemsToRemove)
            {
                newList.Remove(item);
            }
            return newList.ToArray();
        }

        /// <summary>
        /// Checks if the array contains a specified value using the default EqualityComparer.
        /// The main goal is to perform safe contain evaluations even if the array is null.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="array">The source array reference.</param>
        /// <param name="value">The variable target being queried.</param>
        /// <returns>True if the matching value exists.</returns>
        /// <remarks>
        /// Overcomes generic array lookup exceptions specifically validating null references.
        /// </remarks>
        public static bool Contains<T>(this T[] array, T value)
        {
            if (array == null || array.Length == 0)
            {
                return false;
            }

            foreach (T item in array)
            {
                if (EqualityComparer<T>.Default.Equals(item, value))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Determines whether all elements within a boolean collection are true.
        /// The main goal is to evaluate if a series of independent checks succeeded.
        /// </summary>
        /// <param name="array">The source enumerable of boolean states.</param>
        /// <returns>True only if the source is populated and entirely true.</returns>
        /// <remarks>
        /// Useful when validating aggregate results, such as checking if several complex systems fully initialized.
        /// </remarks>
        public static bool AllTrue(this IEnumerable<bool> array)
        {
            if (!array.Any())
            {
                return false;
            }
            return array.All(x => x);
        }

        /// <summary>
        /// Determines whether any elements within a boolean collection are true.
        /// The main goal is to evaluate if at least one independent check succeeded.
        /// </summary>
        /// <param name="array">The source enumerable of boolean states.</param>
        /// <returns>True if the source is populated and at least one item is true.</returns>
        /// <remarks>
        /// Applied widely for flags, or handling generic trigger queries over groups of conditions.
        /// </remarks>
        public static bool AnyTrue(this IEnumerable<bool> array)
        {
            if (!array.Any())
            {
                return false;
            }
            return array.Any(x => x);
        }
    }
}