using System;
using Newtonsoft.Json.Serialization;

namespace GameModuleDTO.Json
{
    /// <summary>
    /// Client-Side Binder:
    /// Deserialization: Converts Backend (mscorlib) -> Unity (CoreLib).
    /// Serialization: Converts Unity (CoreLib) -> Backend (mscorlib).
    /// </summary>
    public class CrossPlatformTypeBinder : ISerializationBinder
    {
        /// <summary>
        /// CLIENT DESERIALIZATION: Reading JSON from the Backend/Old Version.
        /// </summary>
        public Type BindToType(string assemblyName, string typeName)
        {
            // If the JSON contains "System.Private.CoreLib" (from the Server), 
            // swap it to "mscorlib" so Unity/Mono can resolve it.
            string targetAssembly = assemblyName?.Replace("mscorlib", "System.Private.CoreLib");

            // Look up the type in the corrected assembly
            return Type.GetType($"{typeName}, {targetAssembly}", throwOnError: false);
        }

        /// <summary>
        /// CLIENT SERIALIZATION: Sending JSON from Unity to the Backend.
        /// </summary>
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