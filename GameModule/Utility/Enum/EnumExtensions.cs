namespace Utility.Enum
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    
    public static class EnumExtensions
    {
        public static List<string> ToStringList(this Enum enumValue)
        {
            return Enum.GetValues(enumValue.GetType())
                .Cast<Enum>()
                .Select(e => e.ToString())
                .ToList();
        }

        public static Array ToStringArray(this Enum enumValue)
        {
            return Enum.GetValues(enumValue.GetType())
                .Cast<Enum>()
                .Select(e => e.ToString())
                .ToArray();
        }

        public static List<T> ToEnumList<T>(this T enumValue) where T : Enum
        {
            Enum[] test = Enum.GetValues(typeof(T)).Cast<Enum>().ToArray();
            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .ToList();
        }

        public static Array ToArrayList<T>(this T enumValue) where T : Enum
        {
            return Enum.GetValues(typeof(T))
                .Cast<Enum>()
                .ToArray();
        }
        
        public static List<string> ToStringList<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .Select(e => e.ToString())
                .ToList();
        }

        public static string[] ToStringArray<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .Select(e => e.ToString())
                .ToArray();
        }

        public static List<T> ToEnumList<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .ToList();
        }

        public static T[] ToArray<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .ToArray();
        }
    }
}