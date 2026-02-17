using System.Collections;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Utility.Json
{
    public class ShouldSerializeContractResolver : DefaultContractResolver
    {
        public static readonly ShouldSerializeContractResolver Instance = new ShouldSerializeContractResolver();

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty prop = base.CreateProperty(member, memberSerialization);

            // Strings: skip when empty ("")
            if (prop.PropertyType == typeof(string))
            {
                prop.ShouldSerialize = instance =>
                {
                    string? value = prop.ValueProvider.GetValue(instance) as string;
                    // nulls are handled by NullValueHandling.Ignore (recommended), we only enforce empty check here
                    return !string.IsNullOrEmpty(value);
                };
                return prop;
            }

            // Collections: skip when empty (arrays, List<>, Dictionary<,>, HashSet<>, etc.)
            if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string))
            {
                prop.ShouldSerialize = instance =>
                {
                    object? value = prop.ValueProvider.GetValue(instance);
                    if (value == null)
                    {
                        return false; // let NullValueHandling.Ignore handle nulls
                    }

                    // IDictionary: use Count
                    if (value is IDictionary dict)
                    {
                        return dict.Count > 0;
                    }

                    // Non-generic ICollection: use Count
                    if (value is ICollection coll)
                    {
                        return coll.Count > 0;
                    }

                    // Generic IEnumerable: check if there is at least one element
                    if (value is IEnumerable en)
                    {
                        IEnumerator e = en.GetEnumerator();
                        try { return e.MoveNext(); }
                        finally { (e as IDisposable)?.Dispose(); }
                    }

                    // Not a recognizable enumerable type? serialize it
                    return true;
                };
                return prop;
            }

            // Value types (primitives, structs, enums) and Nullable<T>:
            // skip when equal to default(T) (e.g., false, 0, 0.0, '\0', default(DateTime), default(enum), default(struct))
            if (IsValueOrNullableValueType(prop.PropertyType))
            {
                prop.ShouldSerialize = instance =>
                {
                    object? value = prop.ValueProvider.GetValue(instance);
                    if (value == null) 
                    {
                        return false; // NullValueHandling.Ignore
                    }

                    (Type underlying, bool isNullable) = GetUnderlyingType(prop.PropertyType);
                    // Compare to default(underlying)
                    object def = GetDefaultValue(underlying);

                    // Special-case floating zero if you want exact 0 only (not NaN/Infinity); Equals handles 0.0 fine.
                    return !object.Equals(value, def);
                };
                return prop;
            }

            // For all other reference types, default behavior (nulls skipped by settings)
            return prop;
        }

        private static bool IsValueOrNullableValueType(Type t)
        {
            if (t.IsValueType)
            {
                return true;
            }
            return Nullable.GetUnderlyingType(t) != null;
        }

        private static (Type underlying, bool isNullable) GetUnderlyingType(Type t)
        {
            Type? u = Nullable.GetUnderlyingType(t);
            return (u ?? t, u != null);
        }

        private static object GetDefaultValue(Type t)
        {
            // default(T) for value types (including enums/structs); null for ref types (not used here)
            return t.IsValueType ? Activator.CreateInstance(t)! : null!;
        }
    }
}