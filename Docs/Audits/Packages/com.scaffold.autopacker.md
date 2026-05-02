# Audit: `com.scaffold.autopacker`

Audit date: 2026-05-02. Reviewer: senior architect.

## 1. Summary

This package is a **shipping shell** around two precompiled DLLs (`AutoPackerContracts.dll`, `AutoPackerGenerator.dll`) plus four C# files: an `AssemblyInfo.cs` that contains nothing but a `#pragma`, two sample `MonoBehaviour`s, and one Edit Mode test fixture. The actual functionality (`[AutoPack]` attribute, `IPackingHandler`, `IPackedStruct`, the Roslyn source generator) lives outside this package — generator source is at `/home/user/Scaffold/Generators/AutoPacker/src/`. From a code-review standpoint, **the four `.cs` files do not constitute the package**; the audit surface is mostly the README, the test, and the integration choices.

What the package promises is sound and matches the rubric: a source generator that writes `partial` `Pack`/unpack methods and an `unmanaged Packed` struct per `[AutoPack]` type. Compile-time generation, zero reflection at runtime, blittable snapshots — exactly the "minimum code, maximum extensibility, prefer generics" model. The diagnostic CSG002 for managed fields that aren't mapped via `[Packed(typeof(T))]` is a textbook fail-fast.

The problems are at the package boundary, not the design:

- **The runtime is two DLL blobs in the `Runtime/` folder.** No way to inspect, diff, or patch without leaving the Unity project. No version stamp visible from `package.json`. README's "Change Checklist" says "rebuild `Generators/AutoPacker` after editing the generator or contracts; copy/update DLLs under `Runtime/` as the project's pipeline requires" — that pipeline isn't documented anywhere checked in here.
- **`AssemblyInfo.cs` is a single `#pragma`.** It's not an "AssemblyInfo" — it's analyzer-suppression for one rule. Misnamed. Doesn't belong in `Runtime/` where it's part of the user-facing code surface; should be in a generated/internal companion or in an `.editorconfig`.
- **`asmdef` has no references** (`Scaffold.Autopacker.asmdef` is `{ "name": "Scaffold.Autopacker" }`). The contracts DLL is bundled as a precompiled reference (Unity will pick it up by file presence) but there's no explicit reference list, no `autoReferenced: false`, no version-defines guard. Consumers need to know they're depending on `Scaffold.AutoPacker` (the namespace inside the contracts DLL) — a non-obvious mismatch with the asmdef name `Scaffold.Autopacker`.
- **The samples define a `MonoBehaviour` per use case** with `Start()` calling `Debug.Log` for the "tutorial" effect. Fine for a sample, but `CustomPackerUseCase` ships an `EncryptionPacker` that's a security smell named after a real-world primitive (`HashCode` is not encryption); even as a sample, that name will get someone in trouble.
- **The test file declares `[AutoPack] partial class` types alongside the test fixture** in the same file. Useful as a generator integration check; problematic because it makes the test assembly part of the generator's input. Re-running `dotnet test` outside Unity won't pick up the source generator unless the test asmdef wires it. This is brittle.
- **`Idempotent` and `Editor-only` questions:** The generator runs at compile-time inside Unity (and CI), driven by the bundled `AutoPackerGenerator.dll`. It is editor-only in the sense that it produces files at compile time; it is **not** an editor menu or asset-pipeline tool. Idempotency depends on the generator emitting deterministic output across runs — this audit cannot verify that without running the generator and diffing output, which I can't do here.
- **`Hard-coded conventions vs configurable`:** The README implies the only configuration is via the attributes (`[AutoPack]`, `[Packed]`, `[Packed(typeof(T))]`) and `IPackingHandler` extension methods. That's the right amount of configurability for this kind of generator: declarative on the field, behavior on the handler. No global config file.

**Verdict: I cannot fully audit this package without the generator source in-tree.** What's in the four `.cs` files is fine but trivial. The risk profile is "operational" — the binary drop and the rebuild dance are the highest-risk parts and they're undocumented. Treat the rest of this report as findings on what I *can* see.

## 2. Structure

```
com.scaffold.autopacker/
  Runtime/
    AssemblyInfo.cs                    1 line, only `#pragma warning disable SCA0009`
    AutoPackerContracts.dll            precompiled, ~129 bytes
    AutoPackerGenerator.dll            precompiled, ~130 bytes
    Scaffold.Autopacker.asmdef         { "name": "Scaffold.Autopacker" }
  Samples/
    AutoPackerUseCase.cs               minimal-pack/unpack demo
    CustomPackerUseCase.cs             custom IPackingHandler + extension method
    Scaffold.Autopacker.Samples.asmdef
  Tests/
    AutoPackerTests.cs                 6 tests; declares 3 [AutoPack] types inline
    Scaffold.Autopacker.Tests.asmdef
  README.md, package.json
```

The two DLL sizes (~130 bytes each, per `ls -la`) are suspicious — that's stub-sized. Either the listing is misleading or these are placeholders not real generators. **Verify locally**: a real Roslyn source generator DLL is hundreds of KB to a few MB.

If those DLLs are placeholders, the entire package is non-functional in the checked-in state and CI will be relying on something built elsewhere. Investigate before trusting any test pass.

The actual generator project lives at `/home/user/Scaffold/Generators/AutoPacker/{src/AutoPackerGenerator,src/Contracts,AutoPackerGenerator.csproj,AutoPackerGenerator.sln}` — outside the audit scope. The `package.json` does not declare any dependency, so this package is correctly self-contained at the manifest level (`package.json:13`).

## 3. What's good

- **No runtime reflection.** Generated `Pack(handler)` and ctor-from-`Packed` are static, type-checked code.
- **Generic typed handler.** `IPackingHandler.Resolve<TSource, TTarget>(TSource source)` is fully generic — exactly the rubric's "prefer generics for compile-time safety" goal. The generator binds `Resolve(this IPackingHandler, TSource)` extension methods when present (`AutoPackerTests.cs:62-75`), giving consumers per-type override knobs without touching the generated code.
- **Field-level intent is declarative.** `[Packed]` for unmanaged-as-is, `[Packed(typeof(T))]` for managed-converted. Two attributes, no string keys, no positional arguments to remember. Right level of API surface.
- **CSG002 diagnostic is fail-fast.** README claims invalid managed fields trigger CSG002 and skip emission. That's the right behavior — broken types don't compile, you find out at build time. Cannot verify text of the diagnostic without the generator source.
- **`partial`-only contract.** Forces the generator into a non-magic mode; user code stays in the same declaration. This is the standard Roslyn pattern, and it survives refactors better than `T4` or attribute-injection-into-foreign-types.
- **Tests cover the four real scenarios** (`AutoPackerTests.cs`):
  - default packing / round-trip (`:80-95`),
  - non-`[Packed]` field is dropped on unpack (`:97-112`),
  - custom handler intercepts pack and unpack (`:114-145`),
  - extension-method `Resolve` overload binding (`:147-183`).
- **Sample `EncryptionPacker` honestly comments its identity-fallback path** (`CustomPackerUseCase.cs:35`) so a reader sees the `Convert.ChangeType` default. Not great that it's called "encryption" — see 4.5 — but the implementation is honest.

## 4. Issues / smells

### 4.1 The `Runtime/` DLLs may be stub-sized

`Runtime/AutoPackerContracts.dll` (~129 bytes) and `Runtime/AutoPackerGenerator.dll` (~130 bytes) — `ls -la` shows these sizes. A functional Roslyn analyzer/generator assembly is at minimum ~30 KB and typically several hundred KB once Roslyn references are pulled in. If these are real, the file system is lying. If they're not real, **all 6 tests in `AutoPackerTests.cs` will fail to compile** (`PlayerState.Pack()`, `(PlayerState.Packed)packedData`, the `new PlayerState(networkData)` ctor, etc., all come from generator emission). **Action: check `git lfs ls-files` or run the build and confirm.**

### 4.2 No documented build pipeline for refreshing the DLLs

`Generators/AutoPacker/AutoPackerGenerator.csproj` exists at the repo root, but no `build.ps1`, no `.targets`, no copy-on-build step is checked into the package. The README says "copy/update DLLs under `Runtime/` as the project's pipeline requires" (README:154) — which is to say *we don't have one*. This is a maintenance risk: anyone touching the generator must remember a build-and-copy step that doesn't run in CI.

Two reasonable shapes:

- (A) Reference the generator project directly via `nuget`-style packing or a `.targets` MSBuild include in the asmdef's parent, with the DLL copied by build.
- (B) Keep the binary-drop model but check in a `Generators/AutoPacker/build.ps1` that does `dotnet publish` and copies `*.dll` to `Assets/Packages/com.scaffold.autopacker/Runtime/`. Wire it as a `dotnet tool` or CI job.

### 4.3 `AssemblyInfo.cs` is a misleading filename

`Runtime/AssemblyInfo.cs:1`:

```csharp
#pragma warning disable SCA0009 // Assembly attributes are in the global namespace by design
```

That's not assembly metadata — it's a single analyzer suppression. The file should be named `AnalyzerSuppressions.cs` or moved to `.editorconfig`/`Directory.Build.props`. The pragma suppression also applies only to this file (not to DLL-emitted attributes from the generator), which means the suppression is probably *not doing what its author thought*. Either:

- Move the suppression to a `GlobalSuppressions.cs` with `[assembly: SuppressMessage("...", "SCA0009")]`,
- Or use `.editorconfig` `dotnet_diagnostic.SCA0009.severity = none` for this asmdef,
- Or delete the file if the warning doesn't actually fire.

Note: `Scaffold.Addressables.Runtime/AssemblyInfo.cs:3-4` has actual `InternalsVisibleTo` calls — that's the right shape. This package's `AssemblyInfo.cs` should match that shape or be renamed.

### 4.4 `Scaffold.Autopacker.asmdef` is bare

```json
{ "name": "Scaffold.Autopacker" }
```

No `references`, no `autoReferenced` setting, no `defineConstraints`, no `versionDefines`. The contracts DLL alongside it auto-references in Unity, but for IDE rebuilds (Rider/VS using the generated `Scaffold.Autopacker.csproj`), Unity will only include the DLLs as `Reference` items if the asmdef-to-precompiled-reference link is set in inspector. **Verify in Unity's asmdef inspector** whether `AutoPackerContracts.dll` is in the "Assembly Definition References" or "Override References" list. If it's not, consumer projects may show red squigglies in IDE while compiling fine in Unity (or vice versa).

Additionally: `autoReferenced: true` is the default. If the generator is supposed to apply to consumer assemblies, the consumer asmdef must reference `Scaffold.Autopacker`. The README mentions this (README:44) — good — but the asmdef should at least set `noEngineReferences: true` to prove it doesn't need UnityEngine (the contracts DLL likely doesn't).

### 4.5 `EncryptionPacker` is the wrong name

`Samples/CustomPackerUseCase.cs:15-37`. The sample handler is named `EncryptionPacker` but it does `text.GetHashCode()` and `"Decoded_" + hash`. `GetHashCode()` is **not** encryption, it isn't even hashing in the cryptographic sense, and it loses information (you can't recover `Secret` from `int hash` — the `"Decoded_" + hash` reconstruction is the literal string `"Decoded_<int>"`, not the original).

Even as a tutorial, the name implies a security property the code doesn't have. Rename to `HashingPacker` or `LossyMappingPacker` and add a `// Demonstration only — not cryptographically secure` comment. The test fixture uses `MockEncryptionPacker` (`AutoPackerTests.cs:30-54`) with the same problem.

### 4.6 Test fixture re-declares `[AutoPack]` types in the test file

`Tests/AutoPackerTests.cs:8-28` declares three `[AutoPack] partial class` types (`PlayerState`, `SecurePayload`, `ExtendedPayload`) at the file/namespace level, then asserts behavior on them. This works in Unity because the source generator runs over the test asmdef's compilation; it **only works** if the test asmdef references `Scaffold.Autopacker` (which it does via inspector — verify in `.asmdef`'s `references` list — there's no JSON evidence here).

The brittleness: any tooling that compiles the test assembly without the generator (e.g., a coverage run, a fresh IDE that hasn't built generators yet) silently fails. Two stable options:

- (A) Move the test types to a `Tests/Shared/PackedFixtures.cs` file and let the generator pick them up there. Tests reference fixtures, no behavior change.
- (B) Add a sanity test that compiles a known-good generated symbol into a literal, e.g. `Assert.That(typeof(PlayerState).GetNestedType("Packed"), Is.Not.Null);` — fast-fail if the generator didn't run.

### 4.7 `IPackingHandler.Resolve<TSource, TTarget>` is a runtime-typed entry point

The generic signature is compile-time, but the implementation at `CustomPackerUseCase.cs:17-37` and `AutoPackerTests.cs:34-53` does:

```csharp
if (source is string text && typeof(TTarget) == typeof(int)) { ... }
if (source is int hash && typeof(TTarget) == typeof(string)) { ... }
if (source is TTarget target) return target;
return (TTarget)Convert.ChangeType(source, typeof(TTarget));
```

That's runtime type inspection and `Convert.ChangeType` boxing. The generator could (and may, can't see) bind directly to per-type extension methods (the test at `:147-183` proves it does for `Vector2`). If extension binding is preferred, the generic `Resolve<TSource,TTarget>` should be marked as the *fallback* contract, with documentation telling consumers to write extension overloads for hot types. Per the rubric ("prefer generics for compile-time safety"), the runtime typeof-equality cascade is the wrong default for tutorial code; consumers will copy-paste it.

The README (lines 122-123) says: "Provide extension methods on `IPackingHandler` when you need tight control over specific Unity types". Good guidance; should be the dominant path in samples too.

### 4.8 `DefaultPackingHandler` behavior is undocumented

The README (line 31) says `DefaultPackingHandler` "uses `Convert.ChangeType` / identity". But that handler isn't shown anywhere in the visible source — it's inside the contracts DLL. The behavior matters: `Convert.ChangeType` will throw on incompatible pairs, lose precision on numeric narrowing, and accept anything `IConvertible`. A consumer who hits a runtime cast exception in production has no path to diagnose without decompiling the DLL.

Either ship the contracts source in `Generators/AutoPacker/src/Contracts/` *and* in `Runtime/Contracts/` (so it's readable in-place), or document `DefaultPackingHandler`'s behavior with a `## Behavior` subsection in the README that shows: identity fast-path, `Convert.ChangeType` fallback, throws on incompatibility.

### 4.9 The `Pack(IPackingHandler handler)` "uses `DefaultPackingHandler` when handler is null" rule is implicit

README line 31. If a `Pack()` no-arg overload is generated that defaults to `DefaultPackingHandler`, that's a default-value-hides-error pattern only if `DefaultPackingHandler` silently produces wrong results on managed fields not mapped via `[Packed(typeof(T))]`. The README says CSG002 prevents that case at compile time, which restores the fail-fast behavior — good. But the `Pack()` parameterless overload should probably *not exist* for types that have any `[Packed(typeof(T))]` field, since those fields explicitly need a custom handler. Today, calling `Pack()` on `SecureData` would route through `DefaultPackingHandler.Resolve<string, int>` and return whatever `Convert.ChangeType("Hi", typeof(int))` does (throws). At least it throws — but it's runtime, not compile time. Consider emitting `Pack()` only when no `[Packed(typeof(T))]` fields are present, and forcing `Pack(IPackingHandler)` otherwise.

### 4.10 Samples ship `MonoBehaviour`s but the package is data/codegen

`Samples/AutoPackerUseCase.cs:16-37` and `CustomPackerUseCase.cs:47-71` derive from `MonoBehaviour` only to host `Start()` and `Debug.Log`. That's fine for a Unity sample, but the package's actual responsibility (codegen + blittable structs) has nothing to do with `MonoBehaviour`. A pure-C# sample with `[Test]` or a static `Main` would be more honest and would compile in non-Unity contexts.

The `Samples` folder is also under `Samples/`, not `Samples~/` (no tilde). In Unity package convention, `Samples~/` is excluded from compilation by default and exposed via the Package Manager "Import Samples" UI. A bare `Samples/` is always compiled, which means consumers of the package compile your `MonoBehaviour`s into their build. Move to `Samples~/` and add a `samples` array to `package.json`.

## 5. Suggested before/after snippets

### 5.1 Rename `AssemblyInfo.cs` and use a real assembly attribute

**Before** (`Runtime/AssemblyInfo.cs:1`):

```csharp
#pragma warning disable SCA0009 // Assembly attributes are in the global namespace by design
```

**After** (delete the file, add `Runtime/GlobalSuppressions.cs`):

```csharp
using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    category: "Scaffold.Analyzers.Style",
    checkId:  "SCA0009",
    Justification = "Assembly attributes are in the global namespace by design.",
    Scope = "module")]
```

Or, if SCA0009 doesn't actually fire in this asmdef, delete it entirely.

### 5.2 Make the asmdef explicit

**Before** (`Runtime/Scaffold.Autopacker.asmdef`):

```json
{ "name": "Scaffold.Autopacker" }
```

**After:**

```json
{
  "name": "Scaffold.Autopacker",
  "rootNamespace": "Scaffold.AutoPacker",
  "noEngineReferences": true,
  "autoReferenced": false,
  "precompiledReferences": [
    "AutoPackerContracts.dll"
  ]
}
```

`autoReferenced: false` forces consumers to reference `Scaffold.Autopacker` deliberately. `noEngineReferences: true` proves the contracts don't depend on UnityEngine (they shouldn't — packing should be pure C#).

### 5.3 Guard against a missing generator at compile time

Add to `Tests/AutoPackerTests.cs` near the top:

```csharp
[Test]
public void Generator_EmittedNestedPackedType_ForKnownAttributedType()
{
    Type packed = typeof(PlayerState).GetNestedType("Packed");
    Assert.That(packed, Is.Not.Null,
        "Generator did not emit PlayerState.Packed. Verify AutoPackerGenerator.dll is present and the consumer asmdef references Scaffold.Autopacker.");
}
```

Costs nothing; turns a future "all six tests fail with cryptic 'Pack does not exist' errors" into one test failure with an actionable message.

### 5.4 Move samples out of compilation

```
Samples/    -> Samples~/
```

…and `package.json`:

```json
"samples": [
  {
    "displayName": "AutoPacker Use Cases",
    "description": "Pack/unpack and custom handler examples.",
    "path": "Samples~/UseCases"
  }
]
```

## 6. Easy wins (5–8)

1. **Rename or delete `Runtime/AssemblyInfo.cs`** (4.3).
2. **Move `Samples/` to `Samples~/`** and register them in `package.json`'s `samples` array (4.10).
3. **Rename `EncryptionPacker` → `HashingPacker`** in samples and tests (4.5).
4. **Add a generator-presence sanity test** at the top of `AutoPackerTests.cs` (5.3).
5. **Verify the DLL sizes** (4.1) and add `git lfs` or a `.gitattributes` rule for `*.dll` if they're tracked as plain blobs.
6. **Document the rebuild-and-copy step** as a checked-in `Generators/AutoPacker/build.ps1` and reference it from the README's "Change Checklist" (4.2).
7. **Set `autoReferenced: false`** on `Scaffold.Autopacker.asmdef` (5.2).
8. **Add `Behavior` subsection** to the README documenting `DefaultPackingHandler` semantics (4.8).

## 7. Bigger refactors

### 7.1 Bring contracts source in-tree

The contracts surface (`[AutoPack]`, `[Packed]`, `IPackingHandler`, `IPackedStruct`, `IPackable`, `DefaultPackingHandler`) is small and stable. Ship it as readable C# under `com.scaffold.autopacker/Runtime/Contracts/` and remove `AutoPackerContracts.dll`. Consumers get IDE go-to-definition, you get inline review of changes, and the `Generators/AutoPacker/src/Contracts/` becomes a single source of truth (not duplicated).

The generator itself must stay as a DLL (Roslyn requirement), but only one DLL needs to live in `Runtime/`, not two.

### 7.2 Wire the generator build into CI

Add a `.github/workflows` step (or whatever this repo uses) that:

1. `dotnet publish Generators/AutoPacker/AutoPackerGenerator.csproj -c Release`,
2. Verifies the published DLL hash matches the checked-in `AutoPackerGenerator.dll`,
3. Fails CI if they diverge.

This makes "did someone forget to copy the DLL?" a build-time assertion. It's the single highest-leverage change for the package.

### 7.3 Decide: `Pack()` parameterless overload

Per 4.9. Either emit it only when safe, or document that it routes managed-typed-mapped fields through `Convert.ChangeType` and *will* throw at runtime on incompatible types.

### 7.4 Provide a non-Unity test target

The test asmdef compiles inside Unity. The generator is C#-only and could run under `dotnet test` in `Generators/AutoPacker/`. Consider mirroring the test types into a `Generators/AutoPacker/tests/` xUnit project that runs the generator outside Unity. Catches generator regressions before Unity has to round-trip.

## 8. Organization & docs

The README is the strongest part of the package. It documents:

- What the generator emits per type (README:36-41).
- Common mistakes (README:49-53).
- Two worked examples (README:65-109).
- A "Best Practices / Anti-Patterns" pair (README:118-130).
- An "AI Agent Context" section listing invariants and forbidden dependencies (README:144-160).

That's significantly above average. The gaps:

- **No documentation of `DefaultPackingHandler` behavior** (4.8).
- **No documentation of how to rebuild the DLLs** (4.2). The Change Checklist is one-liner: "rebuild `Generators/AutoPacker` after editing the generator or contracts; copy/update DLLs under `Runtime/` as the project's pipeline requires" — which assumes the reader knows the pipeline. Document it concretely (`dotnet publish ... && cp ... ./Runtime/`).
- **`Forbidden Dependencies`** (README:151-152) is correct but should also forbid UnityEngine in the contracts DLL — the contracts are pure C# and should compile without Unity.
- **Naming inconsistency**: package is `com.scaffold.autopacker`, asmdef is `Scaffold.Autopacker`, namespace is `Scaffold.AutoPacker` (capital P), folder is `AutoPacker`. Pick one casing and stick with it. The README mixes them too.
- **Changelog has one entry** (README:169) and points to "Initial README". Either the package is genuinely 0.1.0-greenfield (which the version supports) or the changelog needs catch-up entries for any subsequent generator changes.

## References (Roslyn source generators / Unity asmdef)

- Microsoft, *Source Generators: Cookbook* — `https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md`. Standard guidance: emit deterministic output, register diagnostics with stable IDs (CSG002 here), require `partial` on user types, never edit existing files.
- Microsoft, *Roslyn analyzer and generator distribution* — `https://learn.microsoft.com/visualstudio/extensibility/roslyn-version-support`. DLLs in `analyzers/dotnet/cs/` are picked up by MSBuild; Unity also accepts plain DLLs in package `Runtime/` if labeled with `RoslynAnalyzer` in the meta. Verify the `.dll.meta` of `AutoPackerGenerator.dll` has `labels: [RoslynAnalyzer]` (1226 bytes per `ls`, plausible).
- Unity, *Asmdef precompiled references* — `https://docs.unity3d.com/Manual/class-AssemblyDefinitionImporter.html`. `precompiledReferences` and `overrideReferences: true` are the modern way to bind specific DLLs.
- Unity, *Samples in packages* — `https://docs.unity3d.com/Manual/cus-samples.html`. The `Samples~/` convention (with tilde) excludes the folder from automatic compilation while letting Package Manager import on demand.
- Andrew Lock, *Creating an incremental source generator* — `https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/`. Useful if migrating from `ISourceGenerator` to `IIncrementalGenerator` for build-time perf; can't tell from a binary whether this generator is incremental.
