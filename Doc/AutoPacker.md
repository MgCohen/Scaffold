# AutoPacker Module

## Generator Architecture & Locations

### Source Code
The raw source code and C# project `AutoPackerGenerator.csproj` for the Roslyn analyzer is located outside the Unity Assets folder at:
`[Repository Root]/Generators/AutoPacker/src`

This contains the following structure:
- **`AutoPackerContracts/`**: The interfaces (`IPackingHandler`, `IPackedStruct`) and Attributes (`[AutoPack]`, `[Packed]`). This is built into `Scaffold.AutoPacker.dll`.
  - *WARNING: If you ever change the name of any types, interfaces, or attributes inside Contracts, you MUST remember to update the corresponding hardcoded parsing strings used within the Generator's `AutoPackSyntaxReceiver` and `Emitter` scripts!*
- **`AutoPackerGenerator/`**: The Roslyn compiler plugins.
  - `AutoPackSyntaxReceiver.cs`: The code scanner that walks over the Unity source code during compilation looking for the Attributes and custom `Resolve` extension methods.
  - `Emitter.cs`: The string-builder that dynamically formats the C# struct code and generic assignments.
  - `AutoPackerGenerator.cs`: The entrypoint bridging the SyntaxReceiver findings to the Emitter.

### Published DLLs
When the `AutoPackerGenerator` is built (`dotnet publish -c Release`), the resulting compiled `AutoPackerGenerator.dll` and `Scaffold.AutoPacker.dll` must be exported back into the Unity project so the Unity Compiler can use them. 

The destination for these DLL files is:
`[Repository Root]/Assets/Scripts/Tools/Autopacker/Runtime/`

## Overview
**AutoPacker** is a custom Roslyn Source Generator that lives inside the `Generators/` directory of the Scaffold project. It actively listens to your Unity compilation process to dynamically construct underlying boilerplate code for structs and conversions, entirely removing the need for manual DTO (Data Transfer Object) writing, reflection, or data serialization boilerplate.

By applying simple attributes, developers can define robust zero-allocation packing and unpacking procedures. 

## Basic Usage

### The `[AutoPack]` and `[Packed]` Attributes
To allow the generator to operate on a `class`, it must be declared as `partial` and decorated with the `[AutoPack]` attribute. Individual fields and properties that need to be packaged should receive the `[Packed]` attribute.

```csharp
using Scaffold.AutoPacker;
using UnityEngine;

[AutoPack]
public partial class PlayerState
{
    [Packed] public int Health;
    [Packed] public float Speed;
    [Packed] public Vector3 SpawnPoint;
    
    // Values without [Packed] are completely ignored during the packaging process!
    public string TransientDescription;
}
```

### Auto-Generated Conversion
The Roslyn generator will automatically output a new `PlayerState.Packed.g.cs` file at compile time containing a `struct Packed`.
Additionally, it patches the base class with:
- `public IPackedStruct Pack()` method handling extraction.
- An unpacking constructor matching `PlayerState(PlayerState.Packed packedData)`.
- A default constructor `PlayerState()` if you haven't explicitly declared one!

#### Example
```csharp
var state = new PlayerState { Health = 100, Speed = 5f, SpawnPoint = Vector3.zero };

// Generated: Extracts the assigned variables into a zero-allocation struct representation!
var networkData = state.Pack(); 

// Generated: Re-creates the object locally from the structured layout!
var restoredState = new PlayerState((PlayerState.Packed)networkData); 
```

## Advanced Features

### Custom Packing Handlers (`IPackingHandler`)
Sometimes you need precise control over how data is processed—such as encrypting a string, truncating floating-point precision, or serializing a complex object reference into a simple network ID hash.

To achieve this, simply implement `IPackingHandler` and provide an overridden handler logic:

```csharp
[AutoPack]
public partial class SecurePayload
{
    // A string is a managed type, which cannot exist inside unmanaged packed structs easily.
    // By providing typeof(int), we force the struct generator to allocate an `int` for this value!
    [Packed(typeof(int))] public string Secret; 
}

public class EncryptionPacker : IPackingHandler
{
    public TTarget Resolve<TSource, TTarget>(TSource source)
    {
        if (source == null) return default;
        
        // Intercept Pack (String -> Int)
        if (source is string text && typeof(TTarget) == typeof(int))
            return (TTarget)(object)text.GetHashCode();
            
        // Intercept Unpack (Int -> String)
        if (source is int hash && typeof(TTarget) == typeof(string))
            return (TTarget)(object)("Recreated_" + hash);

        // Fallback for everything else
        if (source is TTarget target) return target;
        return (TTarget)Convert.ChangeType(source, typeof(TTarget));
    }
}
```

You can seamlessly insert your handler directly into the generator's hooks:
```csharp
var payload = new SecurePayload { Secret = "Agent_47" };
var encoder = new EncryptionPacker();

var data = payload.Pack(encoder); // Hashed via the custom IPackingHandler!
var result = new SecurePayload((SecurePayload.Packed)data, encoder); // Unhashed via the custom handler!
```

### Compile-Time Extension Methods
AutoPacker features advanced compile-time dynamic discovery. 
If your custom conversions don't cleanly fit inside a monolithic `Resolve<T, U>` switch statement, you can break them out into beautifully readable C# extension methods!

```csharp
public static class MyNetworkExtensions
{
    // The generator's SyntaxReceiver aggressively sweeps the codebase for any method
    // named `Resolve` bearing `this IPackingHandler`.
    public static int Resolve(this IPackingHandler handler, Vector2 source)
    {
        return (int)source.x * 100;
    }
}
```

The AutoPacker source generator watches your Unity codebase compilation. When it reaches a field designated for packing—e.g., `[Packed(typeof(int))] public Vector2 Coordinate;`—it cross-references your global project. When it explicitly recognizes your custom `public static int Resolve(this IPackingHandler handler, Vector2 source)` signature, it *conditionally rewrites its abstract assignment*.

Rather than generating a restricted generic definition: `Id = handler.Resolve<Vector2, int>(source.Coordinate);`
It dynamically unbinds it to leverage implicit C# type matching: `Id = handler.Resolve(source.Coordinate);`

This forces Unity to securely and cleanly inherit your targeted extension method dynamically during the local compilation procedure!

## Unity Assembly Integration Requirements

To successfully integrate the published `AutoPackerGenerator.dll` into the Unity compiler, we configured its Unity asset settings and surrounding Assembly Definitions as follows:

- **DLL Asset Labels & Toggles:** 
  - Added the `RoslynAnalyzer` label.
  - Added the `RunOnlyOnAssembliesWithReference` and `SourceGenerator` labels.
  - Disabled the "Any platform" and related checkboxes.
  - Disabled the "Auto Reference" and "Validate References" checkboxes so Unity doesn't try assigning it as standard runtime logic.
- **Generator Assembly Definition (.asmdef):**
  - There must be an assembly definition file in the same or a parent folder as the generator DLL inside Unity.
  - All references to Unity assemblies required by the generated code need to be explicitly included there (e.g. `Unity.Burst`, `Unity.Collections`, `Unity.Entities`, `Unity.Entities.Hybrid` if applicable to your project).
  - The `.asmdef` folder needs an accompanying `AssemblyInfo.cs` file (even if it remains completely empty).
- **Marker Attributes:**
  - Static code like the marker attributes (`[AutoPack]`, `[Packed]`) and interfaces (`IPackingHandler`) are added to the Unity Assets directly (or as a separate C# class library referenced by the generator and included in Unity Assets). *Avoid using `IncrementalGeneratorInitializationContext.RegisterPostInitializationOutput` to output these marker attributes dynamically.*
- **Correctness:** 
  - Finally, always make sure the code the generator creates is syntactically valid C#!
