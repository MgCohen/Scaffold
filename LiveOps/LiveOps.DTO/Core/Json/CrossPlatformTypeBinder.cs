using System;
using Newtonsoft.Json.Serialization;

namespace GameModuleDTO.Json
{
    /// <summary>
    /// Provides cross-platform JSON type binding for seamless serialization across boundaries.
    /// </summary>
    /// <remarks>
    /// Deserialization: Converts Backend types to Unity types.
    /// Serialization: Converts Unity types to Backend types.
    /// </remarks>
    public class CrossPlatformTypeBinder : ISerializationBinder
    {
        /// <summary>
        /// Reads JSON from the backend resolving standard runtime components.
        /// </summary>
        /// <param name="assemblyName">The JSON source assembly name.</param>
        /// <param name="typeName">The JSON source type name.</param>
        /// <returns>A mapped execution type reference.</returns>
        public Type BindToType(string assemblyName, string typeName)
        {
            // If the JSON contains "System.Private.CoreLib" (from the Server), 
            // swap it to "mscorlib" so Unity/Mono can resolve it.
            string targetAssembly = assemblyName?.Replace("mscorlib", "System.Private.CoreLib");

            // Look up the type in the corrected assembly
            return Type.GetType($"{typeName}, {targetAssembly}", throwOnError: false);
        }

        /// <summary>
        /// Writes JSON from Unity to the backend resolving standard runtime packages identically.
        /// </summary>
        /// <param name="serializedType">The type executing locally.</param>
        /// <param name="assemblyName">The outbound assembly name parameter.</param>
        /// <param name="typeName">The outbound type name parameter.</param>
        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            // Get the current local assembly name (likely mscorlib in Unity)
            string currentAssembly = serializedType.Assembly.FullName;

            // Swap "mscorlib" out for "System.Private.CoreLib" so the server 
            // recognizes it as a standard .NET type.
            assemblyName = currentAssembly.Replace("System.Private.CoreLib", "mscorlib");
            typeName = serializedType.FullName;
        }
    }
}