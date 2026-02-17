using Utility.List;

namespace Utility.Array
{
    public static class ArrayExtensions
    {
        public static bool IsNullOrEmpty<T>(this T[] array)
        {
            return array == null || array.Length == 0;
        }
        
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

        public static T[] AppendToArray<T>(this T[] originalArray, IEnumerable<T> itemsToAdd)
        {
            List<T> newList = new List<T>(originalArray);
            newList.AddRange(itemsToAdd);
            return newList.ToArray();
        }

        public static T[] RemoveFromArray<T>(this T[] originalArray, T itemToRemove)
        {
            List<T> newList = new List<T>(originalArray);
            newList.Remove(itemToRemove);
            return newList.ToArray();
        }


        public static T[] RemoveFromArray<T>(this T[] originalArray, IEnumerable<T> itemsToRemove)
        {
            List<T> newList = new List<T>(originalArray);
            foreach (T item in itemsToRemove)
            {
                newList.Remove(item);
            }
            return newList.ToArray();
        }
        
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
        
        public static bool AllTrue(this IEnumerable<bool> array)
        {
            if (array.IsNullOrEmpty())
            {
                return false;
            }
            return array.All(x => x);
        }
        
        public static bool AnyTrue(this IEnumerable<bool> array)
        {
            if (array.IsNullOrEmpty())
            {
                return false;
            }
            return array.Any(x => x);
        }
    }
}