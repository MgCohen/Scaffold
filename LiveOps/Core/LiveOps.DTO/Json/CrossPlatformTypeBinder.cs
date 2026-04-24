using System;
using System.IO;
using Newtonsoft.Json.Serialization;

namespace LiveOps.DTO.Json
{

    public class CrossPlatformTypeBinder : ISerializationBinder
    {
        public Type BindToType(string? assemblyName, string typeName)
        {
            if (string.IsNullOrEmpty(assemblyName) || string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            if (!IsAssemblyAllowed(assemblyName))
            {
                throw new InvalidDataException("Disallowed $type assembly in JSON payload: " + assemblyName);
            }

            string targetAssembly = assemblyName.Replace("mscorlib", "System.Private.CoreLib", StringComparison.Ordinal);
            Type? t = Type.GetType($"{typeName}, {targetAssembly}", throwOnError: false);
            if (t != null)
            {
                return t;
            }

            t = Type.GetType($"{typeName}, {assemblyName}", throwOnError: false);
            if (t != null)
            {
                return t;
            }

            t = Type.GetType(typeName, throwOnError: false);
            if (t != null && t.Assembly is { } a && IsAssemblyAllowed(a.GetName().Name))
            {
                return t;
            }

            return null;
        }

        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            string currentAssembly = serializedType.Assembly.FullName;
            assemblyName = currentAssembly.Replace("System.Private.CoreLib", "mscorlib", StringComparison.Ordinal);
            typeName = serializedType.FullName ?? string.Empty;
        }

        private static bool IsAssemblyAllowed(string? name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (name.StartsWith("System.", StringComparison.Ordinal) ||
                name.Equals("System.Private.CoreLib", StringComparison.Ordinal) ||
                name.Equals("mscorlib", StringComparison.Ordinal) ||
                name.Equals("netstandard", StringComparison.Ordinal) ||
                name.StartsWith("LiveOps.", StringComparison.Ordinal) ||
                name.StartsWith("LiveOps.Modules.", StringComparison.Ordinal) ||
                name.StartsWith("LiveOps.Modules", StringComparison.Ordinal) ||
                name.StartsWith("Unity.", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }
    }
}
