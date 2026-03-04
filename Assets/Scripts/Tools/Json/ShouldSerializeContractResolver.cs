using System;
using System.Collections;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Scaffold.Tools.Json
{
    /// <summary>
    /// Custom JSON contract resolver for Newtonsoft JSON serialization.
    /// The main goal is to ignore properties that have default, empty, or null values during serialization to shrink payloads.
    /// </summary>
    /// <remarks>
    /// Attached to global JSON settings to enforce compact network communication across the entire engine.
    /// </remarks>
    public class ShouldSerializeContractResolver : DefaultContractResolver
    {
        /// <summary>
        /// Returns a static singleton instance of the resolver to avoid recreation costs.
        /// The main goal is to provide a single, globally accessible resolver object.
        /// </summary>
        /// <remarks>
        /// Used when configuring JsonSerializerSettings.
        /// </remarks>
        public static ShouldSerializeContractResolver Instance { get; } = new ShouldSerializeContractResolver();

        /// <summary>
        /// Intercepts the property creation process to inject conditional serialization logic.
        /// The main goal is to evaluate string emptiness, collection sizes, and default value types, returning a conditional serialization property.
        /// </summary>
        /// <param name="member">The reflected member metadata.</param>
        /// <param name="memberSerialization">The serialization type constraints.</param>
        /// <returns>A modified JsonProperty indicating whether this member should serialize.</returns>
        /// <remarks>
        /// Core logic filtering out useless properties (like Count 0 lists) dynamically safely.
        /// </remarks>
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
                        try
                        {
                            return e.MoveNext();
                        }
                        finally
                        {
                            (e as IDisposable)?.Dispose();
                        }
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

        /// <summary>
        /// Checks if a given reflection type corresponds to a struct or nullable wrapper.
        /// The main goal is to identify value-types that need explicit default value comparisons.
        /// </summary>
        /// <param name="t">The runtime type instance representing a property mapping dynamically.</param>
        /// <returns>True if the type targets a basic value type structure safely.</returns>
        /// <remarks>
        /// Protects against casting references down into value matching functions.
        /// </remarks>
        private static bool IsValueOrNullableValueType(Type t)
        {
            if (t.IsValueType)
            {
                return true;
            }
            return Nullable.GetUnderlyingType(t) != null;
        }

        /// <summary>
        /// Extracts the core underlying definition type beneath nullable logic.
        /// The main goal is to strip Nullable constraints enabling raw comparisons dynamically.
        /// </summary>
        /// <param name="t">The query type.</param>
        /// <returns>A tuple carrying the raw matched underlying base type and a boolean tracking standard nullability.</returns>
        /// <remarks>
        /// Simplifies extracting generic enum values wrapped in "?" annotations.
        /// </remarks>
        private static (Type underlying, bool isNullable) GetUnderlyingType(Type t)
        {
            Type? u = Nullable.GetUnderlyingType(t);
            return (u ?? t, u != null);
        }

        /// <summary>
        /// Safely evaluates default initialized bounds across any value type via Activator logic arrays natively.
        /// The main goal is to create a dynamic 'default(T)' instances matching variable target types.
        /// </summary>
        /// <param name="t">Target evaluation definition type mapping parameter.</param>
        /// <returns>An untyped native object initialized with memory cleared base variables.</returns>
        /// <remarks>
        /// A core function empowering JSON property checks ensuring values aren't needlessly pushed to payloads.
        /// </remarks>
        private static object GetDefaultValue(Type t)
        {
            // default(T) for value types (including enums/structs); null for ref types (not used here)
            return t.IsValueType ? Activator.CreateInstance(t)! : null!;
        }
    }
}
