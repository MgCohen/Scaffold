# com.scaffold.records — Audit

## Summary
A package whose entire runtime is a one-line shim that adds `IsExternalInit` so `init` setters compile on older TFMs. Everything else is empty. The samples don't even use the shim — they use `init` on a `private struct` that any modern Unity already supports without help. **Verdict: probably delete; if kept, document why and shrink to the minimum.**

## Structure
```
com.scaffold.records/
  Runtime/
    IsExternalInit.cs                      (shim)
    Scaffold.Records.asmdef                (rootNamespace empty, autoReferenced)
  Samples/RecordsUseCases.cs
  Tests/                                   (empty)
  package.json, README.md
```
Two declared namespaces, one of which is `Scaffold.Records` and is empty (`Runtime/IsExternalInit.cs:1-3`).

## What's good
- It's small. Hard to break what isn't there.
- `IsExternalInit` is the canonical workaround for `init`-only properties on .NET Standard 2.0 / older C# versions (Microsoft documented technique).

## Issues / smells

### Existence justification
- Unity 6000 ships C# 9 / .NET Standard 2.1 — `System.Runtime.CompilerServices.IsExternalInit` already exists. The shim risks **TypeForwardedTo** ambiguity when consumed alongside libraries that bring their own. With `autoReferenced: true` (`Runtime/Scaffold.Records.asmdef:18`), the shim is forced into every assembly in the project that doesn't opt out.
- Verify by running a simple `record class Foo(int X);` in a regular Scaffold assembly **without** referencing `Scaffold.Records`. If it compiles, this package is obsolete.

### Asmdef hygiene
- `rootNamespace: ""` (`Runtime/Scaffold.Records.asmdef:3`) — every other package sets it to `Scaffold.<Name>`. Set to `Scaffold.Records`.
- `autoReferenced: true` — actively dangerous for a shim: every consumer gets the type whether they want it or not.
- `noEngineReferences` not set; the shim has zero Unity dependency. Set `true`.

### Samples don't exercise the package
- `Samples/RecordsUseCases.cs:19-23` declares a `private struct PersonRecord` with `init`-only properties. This compiles in any modern Unity without `IsExternalInit` from this package — `Samples/` is in its own asmdef which references the runtime, so the symbol resolves, but a sample should *demonstrate the package's value*. As-is, it demonstrates nothing.

### Empty namespace declaration
- `Runtime/IsExternalInit.cs:1-3` — `namespace Scaffold.Records { }` exists with zero contents. Delete.

### Tests
- Tests folder exists with no `.cs` files. Hard to test a shim, but at minimum: a smoke test that records a `record` declared in another asmdef compile + behave.

## Decision tree

1. **Does Unity 6000 provide `IsExternalInit` already in this project?** Confirmed for `.NET Standard 2.1` and `.NET Framework 4.x` profiles in current Unity versions. → delete the package.
2. **Is the project pinned to .NET Standard 2.0 anywhere?** (Probably not — `com.scaffold.model` references `CommunityToolkit.Mvvm` source generators which require modern compiler.) → still likely deletable.
3. **If kept** for redundancy/safety: shrink to one file, set `autoReferenced: false`, `noEngineReferences: true`, `internal` if a generator can do it that way (it can't — must be `public`), and document the exact scenario it covers in README.

## Suggested before/after

**If you keep it, minimum shape:**
```csharp
// Runtime/IsExternalInit.cs
#if !NET5_0_OR_GREATER && !NETSTANDARD2_1
namespace System.Runtime.CompilerServices
{
    public static class IsExternalInit { }
}
#endif
```
…with asmdef `autoReferenced: false`, `noEngineReferences: true`, `rootNamespace: "Scaffold.Records"`.

**If you delete it:** remove the package, delete its dep from any consumer's `package.json`, and ensure `record` types still compile project-wide.

## Easy wins
1. Verify project compiles `record class` / `init` *without* this package referenced — if yes, schedule deletion.
2. Set `autoReferenced: false`, `noEngineReferences: true`, `rootNamespace: "Scaffold.Records"` in `Runtime/Scaffold.Records.asmdef`.
3. Wrap `IsExternalInit` in a `#if !NET5_0_OR_GREATER && !NETSTANDARD2_1` guard (`Runtime/IsExternalInit.cs:5-8`).
4. Delete the empty `namespace Scaffold.Records {}` block (`:1-3`).
5. Make `Samples/RecordsUseCases.cs` actually demonstrate `record class` (with positional params) — the current `private struct` example doesn't justify the package.

## Organization & docs
- README must explicitly state the supported TFM matrix and why this shim is needed (or note it's vestigial).
- The package name "Records" implies record-type utilities (factories, deconstruction helpers, JSON converters). If that's the long-term intent, scope it. If not, rename or drop.
- This is the smallest package in the audit; before refactoring, decide intent first.

## Consumers

**Zero consumers reference `Scaffold.Records` in `Assets/`.** No `using Scaffold.Records;`, no asmdef references, no `package.json` deps from other packages. Even the consumers who *do* use `init`-only properties don't reach for this package.

The damning evidence: **the project re-implements the shim twice independently in `com.scaffold.states`**:

**`com.scaffold.states/Tests/IsExternalInit.cs:1-7`**:
```csharp
// Enables positional records for assemblies compiled with C# 9 without built-in IsExternalInit.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
```

**`com.scaffold.states/Samples/IsExternalInit.cs:1-7`** — identical file, also `internal`.

This is the strongest possible proof that the package's value proposition has failed:
1. A neighboring Scaffold package needed `IsExternalInit`, didn't know `com.scaffold.records` existed, and re-implemented it.
2. The reimpl is **`internal`**, which is the correct visibility — `com.scaffold.records` ships it `public`, polluting every consumer's namespace via `autoReferenced: true`.
3. Two copies exist within `com.scaffold.states` alone (Tests + Samples asmdefs), confirming the type is a per-assembly ceremony, not a shared library concern.

If the project compiles `record` types in modern Unity without referencing this package (the audit's decision-tree step 1), then `com.scaffold.states` is also compiling its `record` types fine without needing either `Scaffold.Records` or its own `IsExternalInit.cs`. The shims in `com.scaffold.states` are themselves likely dead code.

**Action:** delete `com.scaffold.records`. Then confirm whether `com.scaffold.states/{Tests,Samples}/IsExternalInit.cs` can also be deleted — they're redundant with Unity 6000's built-in.

## Alternatives & prior art

- **Built-in `System.Runtime.CompilerServices.IsExternalInit`** — present in .NET 5+ and .NET Standard 2.1. `https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.isexternalinit`. **Adopt (delete this package)**: Unity 6000 ships .NET Standard 2.1; the type is already there. No shim needed.
- **PolySharp** — Roslyn source generator that emits `IsExternalInit` (and 100+ other polyfills) only when the target framework lacks them, conditionally and `internal` per-assembly. `https://github.com/Sergio0694/PolySharp`. **Adopt** if any pre-2021 Unity version is supported. Solves the "two copies in `com.scaffold.states`" problem automatically and per-assembly.
- **Manual per-assembly `internal IsExternalInit`** — exactly what `com.scaffold.states` did. `https://blog.ndepend.com/using-csharp-9-record-and-init-property-in-your-net-framework-4-x-net-standard-and-net-core-projects/`. **Steal pattern** if PolySharp is rejected: each consuming asmdef gets its own `internal` shim; no package needed.
- **C# 9 records native** — Unity 6000 supports them out of the box. `https://docs.unity3d.com/Manual/CSharpCompiler.html`. **Build (current path) — but argues for deletion**: the package has no future once you confirm Unity 6000 compiles records natively in this project.

Verdict: this is the clearest **delete** candidate in the audit set. There is no scenario where this package's `public IsExternalInit` is the right tool over either Unity's built-in or a per-assembly `internal` polyfill.

## Benchmark plan

There is nothing to benchmark — the type is a marker with zero runtime behavior. The relevant validation is **compile-time only**:

- **Compile-time presence test**
  - What: confirm `record class Foo(int X);` compiles in a Scaffold asmdef that does NOT reference `com.scaffold.records`.
  - Tool: a CI build step (`dotnet build` on the generated csprojs, or Unity's `BuildPipeline.BuildAssetBundles` smoke).
  - Location: `Tests/EditMode/RecordsCompilationProbe.cs` — file declares a `public record Probe(int X);` and asserts it instantiates.
  - Scenario: build with package referenced, then build with package un-referenced. Both should succeed on Unity 6000.
  - Baseline: with package referenced, succeeds (today).
  - Success: also succeeds without the package — proves deletability.

- **Type-forwarding ambiguity probe**
  - What: confirm no `CS0436` ("type conflicts with imported type") warning when `com.scaffold.records` is referenced alongside any other lib that ships its own `IsExternalInit` (e.g., a NuGet package).
  - Tool: Unity console grep for `CS0436` after a clean build.
  - Location: build log assertion in CI.
  - Baseline: `autoReferenced: true` + `public` shim is the riskiest combination; once we hit a NuGet conflict it'll surface here.
  - Success: no CS0436 with the package's `autoReferenced: false` change applied — or, simpler, with the package deleted.

If the package is deleted (recommended), no benchmarks are needed.
