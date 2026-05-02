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
