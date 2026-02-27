using System;
using Newtonsoft.Json.Serialization;

namespace Scaffold.CloudModules
{
    /// <summary>
    /// Client-Side Binder for Newtonsoft JSON Deserialization/Serialization.
    /// The main goal is to convert assembly types between Backend (mscorlib) and Unity (CoreLib).
    /// It is used by the JSON serializer whenever types are shared across the client/server boundary to ensure smooth parsing.
    /// </summary>
    public class CrossPlatformTypeBinder : ISerializationBinder
    {
        /// <summary>
        /// Resolves a requested type across domain assemblies.
        /// The main goal is to map mscorlib to System.Private.CoreLib during deserialization.
        /// It is used strictly by the Newtonsoft JSON internal type resolver.
        /// </summary>
        public Type BindToType(string assemblyName, string typeName)
        {
            string targetAssembly = assemblyName?.Replace("mscorlib", "System.Private.CoreLib");
            return Type.GetType($"{typeName}, {targetAssembly}", throwOnError: false);
        }

        /// <summary>
        /// Projects a concrete type to an assembly and type name pair.
        /// The main goal is to alias CoreLib back to mscorlib.
        /// It is used strictly by the Newtonsoft JSON internal type serializers when sending out strings.
        /// </summary>
        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            string currentAssembly = serializedType.Assembly.FullName;
            assemblyName = currentAssembly.Replace("System.Private.CoreLib", "mscorlib");
            typeName = serializedType.FullName;
        }
    }
}