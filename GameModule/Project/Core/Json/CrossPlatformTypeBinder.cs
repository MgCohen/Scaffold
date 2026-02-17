using Newtonsoft.Json.Serialization;
using System;

/// <summary>
/// A custom SerializationBinder that handles type resolution between different .NET runtimes
/// by explicitly swapping 'System.Private.CoreLib' and 'mscorlib' assembly names.
/// This ensures compatibility between platforms like modern .NET (Windows/Server) and Mono (Unity).
/// </summary>
public class CrossPlatformTypeBinder : ISerializationBinder
{
    /// <summary>
    /// Called during deserialization (reading JSON).
    /// </summary>
    public Type BindToType(string assemblyName, string typeName)
    {
        // When reading JSON, if we see 'System.Private.CoreLib' (from a modern platform),
        // we replace it with 'mscorlib' so the Unity/Mono runtime can find the type.
        string correctedAssemblyName = assemblyName.Replace("System.Private.CoreLib", "mscorlib");

        return Type.GetType($"{typeName}, {correctedAssemblyName}", throwOnError: false);
    }

    /// <summary>
    /// Called during serialization (writing JSON).
    /// </summary>
    public void BindToName(Type serializedType, out string assemblyName, out string typeName)
    {
        // When writing JSON, we replace the local 'mscorlib' with 'System.Private.CoreLib'.
        // This creates a JSON string that is compatible with modern .NET server environments.
        assemblyName = serializedType.Assembly.FullName.Replace("mscorlib", "System.Private.CoreLib");
        typeName = serializedType.FullName;
    }
}