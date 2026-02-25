namespace Utility.Enum
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Provides extension methods for converting enums to lists, arrays, and string representations.
    /// The main goal is to simplify reflection and iteration operations on enum types.
    /// </summary>
    /// <remarks>
    /// Used often in UI dropdowns, serialization workflows, and dynamic value selections to retrieve all possible enum variations quickly.
    /// </remarks>
    public static class EnumExtensions
    {
        /// <summary>
        /// Converts the given enum value's type into a list of its string names.
        /// The main goal is to get all string names of the enum type.
        /// </summary>
        /// <param name="enumValue">An instance of the enum to reflect upon.</param>
        /// <returns>A list of string names representing the enum values.</returns>
        /// <remarks>
        /// Used when an enum instance is available but the generic type parameter is unknown or cumbersome.
        /// </remarks>
        public static List<string> ToStringList(this Enum enumValue)
        {
            return Enum.GetValues(enumValue.GetType())
                .Cast<Enum>()
                .Select(e => e.ToString())
                .ToList();
        }

        /// <summary>
        /// Converts the given enum value's type into an array of its string names.
        /// The main goal is to retrieve an array of enum names from an instance.
        /// </summary>
        /// <param name="enumValue">An instance of the enum to reflect upon.</param>
        /// <returns>An array of string representations.</returns>
        /// <remarks>
        /// Used typically when interacting with older APIs that require arrays rather than lists.
        /// </remarks>
        public static Array ToStringArray(this Enum enumValue)
        {
            return Enum.GetValues(enumValue.GetType())
                .Cast<Enum>()
                .Select(e => e.ToString())
                .ToArray();
        }

        /// <summary>
        /// Converts the given strongly-typed enum value into a list of all its possible enum values.
        /// The main goal is to extract all defined values of the same enum type as a list.
        /// </summary>
        /// <typeparam name="T">The type of the enum.</typeparam>
        /// <param name="enumValue">The enum instance.</param>
        /// <returns>A list of all values defined in the enum type.</returns>
        /// <remarks>
        /// Used for iterating through all possible options of a given enum type easily.
        /// </remarks>
        public static List<T> ToEnumList<T>(this T enumValue) where T : Enum
        {
            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .ToList();
        }

        /// <summary>
        /// Converts the given strongly-typed enum value into an array of all its possible enum values.
        /// The main goal is to provide an array containing all elements of the enum type.
        /// </summary>
        /// <typeparam name="T">The type of the enum.</typeparam>
        /// <param name="enumValue">The enum instance.</param>
        /// <returns>An array of all values defined in the enum type.</returns>
        /// <remarks>
        /// Used primarily in array-based algorithms that need to scan generic enum types.
        /// </remarks>
        public static Array ToArrayList<T>(this T enumValue) where T : Enum
        {
            return Enum.GetValues(typeof(T))
                .Cast<Enum>()
                .ToArray();
        }
        
        /// <summary>
        /// Gets a list of string names for all values defined in the specified enum type.
        /// The main goal is to provide type-safe string mapping of enum values.
        /// </summary>
        /// <typeparam name="T">The enum type to inspect.</typeparam>
        /// <returns>A list of string representations for the enum values.</returns>
        /// <remarks>
        /// Commonly used directly with type parameters, rather than instance extensions.
        /// </remarks>
        public static List<string> ToStringList<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .Select(e => e.ToString())
                .ToList();
        }

        /// <summary>
        /// Gets an array of string names for all values defined in the specified enum type.
        /// The main goal is to expose all enum string values in an array.
        /// </summary>
        /// <typeparam name="T">The enum type to inspect.</typeparam>
        /// <returns>An array of string representations for the enum values.</returns>
        /// <remarks>
        /// Used for UI rendering like generating option fields dynamically.
        /// </remarks>
        public static string[] ToStringArray<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .Select(e => e.ToString())
                .ToArray();
        }

        /// <summary>
        /// Retrieves a generic list of all enumerated values for the specified enum type.
        /// The main goal is to fetch a type-safe List collection of the enum.
        /// </summary>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <returns>A generic List of type T containing all enum value options.</returns>
        /// <remarks>
        /// Used extensively to initialize default state definitions mapping every enum element.
        /// </remarks>
        public static List<T> ToEnumList<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .ToList();
        }

        /// <summary>
        /// Retrieves a strongly typed array of all values within the generic enum type.
        /// The main goal is to allow type-safe iteration over enumerations.
        /// </summary>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <returns>An array of all defined enum states.</returns>
        /// <remarks>
        /// Used often in foreach loops directly operating on the generic parameter.
        /// </remarks>
        public static T[] ToArray<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .ToArray();
        }
    }
}