# com.scaffold.autopacker

# Scaffold AutoPacker

## TL;DR

- Purpose: Roslyn source generator that emits pack/unpack code for `[AutoPack]` partial types, producing an unmanaged `Packed` struct for each type.
- Location: contracts and precompiled generator bits under `Assets/Packages/com.scaffold.autopacker/Runtime/`; generator source lives in `Generators/AutoPacker/` in this repository.
- Depends on: none at package level; consuming projects reference `Scaffold.Autopacker` and compile with the generator.
- Used by: gameplay or infra code that needs blittable-friendly snapshots (network payloads, save buffers, etc.).
- Runtime/Editor: compile-time generation; runtime uses generated structs and `Scaffold.AutoPacker` contract types.
- Keywords: source generator, packing, unmanaged struct, IPackingHandler, partial class.

## Responsibilities

- Owns `[AutoPack]` / `[Packed]` attributes, `IPackingHandler`, `IPackedStruct`, `IPackable`, and `DefaultPackingHandler`.
- Owns generated partial members: nested `Packed` struct implementing `IPackedStruct`, `Pack(...)`, constructor from `Packed`, and optional parameterless constructor for classes.
- Emits `AutoPackerRegistry.g.cs` listing processed types.
- Does not own transport, file formats, encryption, or Unity serialization (`SerializeField`); those stay in consuming modules or custom `IPackingHandler` implementations.
- Boundary: generator enforces that each packed field maps to an **unmanaged** storage type (or a `[Packed(typeof(T))]` target type that is unmanaged); invalid fields produce diagnostic **CSG002**.

## Public API

| Symbol | Purpose | Inputs | Outputs | Failure behavior |
|---|---|---|---|---|
| `[AutoPack]` | Marks a `partial` class/struct for code generation | type declaration | n/a | type ignored if not partial or fields invalid |
| `[Packed]` | Marks a field included in `Packed` | field on `[AutoPack]` type | n/a | CSG002 if field type is not unmanaged and no valid target type |
| `[Packed(typeof(T))]` | Stores field as unmanaged type `T` using handler conversions | field + target `T` | n/a | CSG002 if `T` is not unmanaged |
| `IPackingHandler` | Custom pack/unpack conversions | `Resolve<TSource,TTarget>(source)` | converted value | consumers define behavior; `DefaultPackingHandler` uses `Convert.ChangeType` / identity |
| `IPackedStruct` | Marker for generated packed structs | n/a | `PackedType` | n/a |
| `IPackable.Pack` | Implemented by generated partial | optional handler | `IPackedStruct` | uses `DefaultPackingHandler` when handler is null |
| Extension methods on `IPackingHandler` | Optional per-type `Resolve` overloads the generator binds for custom pack/unpack | see tests / samples | per method | must match shapes the emitter discovers |

Generated per type (names illustrative):

| Symbol | Purpose | Inputs | Outputs | Failure behavior |
|---|---|---|---|---|
| `YourType.Pack(handler)` | Build packed snapshot | optional `IPackingHandler` | `YourType.Packed` as `IPackedStruct` | uses handler for mapped fields |
| `new YourType(YourType.Packed, handler)` | Restore instance from packed data | packed struct, optional handler | instance | non-packed fields get default values |
| `YourType.Packed` | Unmanaged struct fields | n/a | blittable layout | n/a |

## Setup / Integration

1. Add an assembly reference to `Scaffold.Autopacker` (`Runtime/Scaffold.Autopacker.asmdef`) from your asmdef.
2. Ensure the Roslyn generator runs: in this repo the generator ships as `AutoPackerGenerator.dll` beside the contracts DLL under `Runtime/`; rebuilding `Generators/AutoPacker` updates those binaries when you change the generator.
3. Declare `partial` types only; annotate with `[AutoPack]` and mark fields with `[Packed]`.
4. Recompile; inspect generated `*.Packed.g.cs` and `AutoPackerRegistry.g.cs` under `obj` / IDE file list if needed.

**Common mistakes**

- Forgetting `partial` on the type (nothing is generated).
- Marking managed fields without `[Packed(typeof(unmanagedTarget))]` → CSG002.
- Expecting non-`[Packed]` fields to round-trip through `Packed` (they are omitted).

## How to Use

1. Define a `partial` class or struct for your payload.
2. Add `[AutoPack]` to the type and `[Packed]` to each field that must appear in the unmanaged struct.
3. For fields whose runtime type differs from the stored type, use `[Packed(typeof(UnmanagedT))]` and supply an `IPackingHandler` (and optional extension methods) to convert on pack/unpack.
4. Call `instance.Pack()` (or `Pack(handler)`) to obtain `IPackedStruct`; cast to `YourType.Packed` when you need concrete fields.
5. Reconstruct with `new YourType((YourType.Packed)data)` or pass the same handler to the constructor overload that accepts `IPackingHandler` when you used custom conversions.

## Examples

### Minimal

```csharp
using Scaffold.AutoPacker;

[AutoPack]
public partial class PlayerSnapshot
{
    [Packed] public int Health;
    [Packed] public float Speed;
    // Not packed — default/uninitialized after unpack
    public string SessionLabel;
}

// Pack → unmanaged struct → reconstruct
var packed = (PlayerSnapshot.Packed)original.Pack();
var restored = new PlayerSnapshot(packed);
```

### Realistic (custom handler)

```csharp
using Scaffold.AutoPacker;

[AutoPack]
public partial class SecurePayload
{
    [Packed(typeof(int))] public string Secret; // stored as int in Packed
}

public sealed class HashPacker : IPackingHandler
{
    public TTarget Resolve<TSource, TTarget>(TSource source)
    {
        if (source is string s && typeof(TTarget) == typeof(int))
            return (TTarget)(object)s.GetHashCode();
        if (source is int i && typeof(TTarget) == typeof(string))
            return (TTarget)(object)$"decoded_{i}";
        return default;
    }
}

// var packed = payload.Pack(new HashPacker());
// var back = new SecurePayload((SecurePayload.Packed)packed, new HashPacker());
```

### Guard / error path

```csharp
// Invalid: managed field without unmanaged mapping — generator emits CSG002 and skips emission for that type.
// [AutoPack] public partial class Bad { [Packed] public string Direct; }
```

## Best Practices

- Keep `Packed` structs small and stable; they are your contract for network or disk.
- Use `[Packed(typeof(...))]` plus explicit handlers for strings and other managed data instead of widening the struct.
- Provide extension methods on `IPackingHandler` when you need tight control over specific Unity types (see `AutoPackerTests` for `Vector2` examples).
- Prefer explicit handlers for anything security-sensitive; `DefaultPackingHandler` is generic conversion only.
- After changing fields, rebuild and re-run tests that cover pack/unpack.

## Anti-Patterns

- Storing managed references inside generated `Packed` structs (violates unmanaged constraint; use CSG002 as signal).
- Relying on non-packed fields to survive a pack/unpack cycle.
- Using AutoPacker as a full game save format without versioning and migration (add that in a higher layer).

## Testing

- Test assembly: `Scaffold.Autopacker.Tests` (Edit Mode).
- Run from repo root:

```powershell
& ".\.agents\scripts\run-editmode-tests.ps1" -AssemblyNames "Scaffold.Autopacker.Tests"
```

- Expected: all tests pass, zero failures.
- Bugfix rule: add or update a regression test in `Assets/Packages/com.scaffold.autopacker/Tests/` before fixing generator behavior.

## AI Agent Context

- Invariants:
  - packed fields must be unmanaged (or mapped via `[Packed(typeof(T))]` with unmanaged `T`).
  - generated types remain `partial`; user code stays in the same declaration.
- Allowed Dependencies:
  - consuming assemblies may reference only `Scaffold.Autopacker` contracts unless they also implement handlers.
- Forbidden Dependencies:
  - generator must not introduce hidden references to gameplay modules; keep contracts in `Scaffold.AutoPacker` namespace.
- Change Checklist:
  - rebuild `Generators/AutoPacker` after editing the generator or contracts; copy/update DLLs under `Runtime/` as the project’s pipeline requires.
  - run `Scaffold.Autopacker.Tests`.
  - verify CSG002 still triggers for managed fields without mapping.
- Known Tricky Areas:
  - extension method binding for `IPackingHandler`—signature must match what `Emitter` discovers.
  - unpack restores only `[Packed]` fields; document defaults for everything else.

## Related

- `../../../Architecture.md`
- `../../../Generators/AutoPacker/` (generator and contract sources)
- `../../../Docs/Testing/Testing.md`

## Changelog

- `2026-03-31`: Initial README documenting generator flow, attributes, handlers, and tests.
