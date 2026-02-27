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
        public Type BindToType(string assemblyName, string typeName)
        {
            string targetAssembly = assemblyName?.Replace("mscorlib", "System.Private.CoreLib");
            return Type.GetType($"{typeName}, {targetAssembly}", throwOnError: false);
        }

        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            string currentAssembly = serializedType.Assembly.FullName;
            assemblyName = currentAssembly.Replace("System.Private.CoreLib", "mscorlib");
            typeName = serializedType.FullName;
        }
    }
}