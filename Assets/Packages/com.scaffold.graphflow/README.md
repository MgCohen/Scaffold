# Scaffold GraphFlow

## TL;DR

- **`com.scaffold.graphflow`** ships **`Scaffold.GraphFlow.PackageAttributes`** (assembly-level `[GraphPackage]`, port conventions, port tags) and **`Scaffold.GraphFlow.PackageGenerator`** (Roslyn source generator for ExecPlan v2).
- A **graph package** is declared with **`[assembly: GraphPackage]`** on the consumer assembly that owns payloads and the runner; the generator reads that attribute and emits editor/runtime/registry code (M1+).
- Consumers must **reference the attributes assembly**, **reference the generator DLL** from the same asmdef (explicit analyzer reference), and **meet the requirements** in [Setup / Integration](#setup--integration).

## Responsibilities

### Owns

- Attribute definitions for graph package configuration and future port discovery (`GraphPackageAttribute`, `PortConvention`, `GraphPortAttribute`, etc.).
- The Roslyn **incremental** source generator that interprets `[assembly: GraphPackage]` and emits generated C# for the owning assembly.

### Does not own

- Runtime graph execution (`GraphRunner`, `GraphController`, bakers, importers) — that lives in **`Scaffold.GraphFlow.M0`** and future **`Scaffold.GraphFlow`** runtime packages.
- Graph Toolkit editor graphs, Unity assets, or Card Framework types.

### Boundaries

- **Attributes:** pure .NET Standard 2.0, **no Unity references** (`noEngineReferences` on **`Scaffold.GraphFlow.PackageAttributes.asmdef`**, assembly name **`Scaffold.GraphFlow.PackageAttributes`**, precompiled **`Scaffold.GraphFlow.PackageAttributes.dll`** — avoids Unity treating a plugin DLL basename as colliding with asmdef naming).
- **Generator:** runs only at compile time; emitted code targets the **consumer’s** language version (keep emissions C# 9–compatible unless the consumer raises LangVersion).

## Public API

| Symbol | Purpose | Inputs | Outputs / effects |
| --- | --- | --- | --- |
| `GraphPackageAttribute` | Declares one graph package per assembly (repeatable). | Assembly-level attribute; set via named properties. | Generator discovers configuration; M1+ emits types. |
| `GraphPackageAttribute.Runner` | Package discriminator; ties payloads and codegen to `GraphRunner` subclass. | `typeof(MyRunner)` | Required. |
| `GraphPackageAttribute.Extension` | File extension for GT source files (no leading dot). | string | Required for importer/menu wiring in full parity. |
| `GraphPackageAttribute.AssetMenu` | Project-window create menu path. | string | Required for authoring UX in full parity. |
| `GraphPackageAttribute.Convention` | Port discovery strategy for payloads. | `PortConvention` enum | Required when generating dispatcher/listener ports. |
| `GraphPackageAttribute.RegistryNamespace` | Namespace for generated registry and node types. | string | Required when emitting generated types. |
| `PortConvention` | Strategy enum (`CommandResultPair`, `AttributedFields`, etc.). | — | Enumerates built-in strategies (ExecPlan v2). |
| `GraphPortAttribute`, `GraphHiddenAttribute`, `GraphMenuAttribute`, `InAttribute`, `OutAttribute` | Future port/authoring metadata. | Per ExecPlan v2 | Used by generator and tooling as M1+ grows. |

**Failure behavior:** If `Runner` is missing or invalid, the generator skips or cannot emit meaningful output for that assembly. Empty `Extension` / `RegistryNamespace` may be accepted for bootstrap placeholders but are **not** sufficient for a shippable graph package.

## Setup / Integration

### Graph package requirements (checklist)

Apply these to **each assembly** that declares `[assembly: GraphPackage(...)]`:

1. **References**
   - Reference assembly **`Scaffold.GraphFlow.PackageAttributes`** (add **`Scaffold.GraphFlow.PackageAttributes.asmdef`** or equivalent).
   - Add an asmdef reference to the **Roslyn generator** plugin: the **GUID** of `Scaffold.GraphFlow.PackageGenerator.dll` in `.meta` (label: Roslyn analyzer, **Run Only On Assemblies With Reference**, explicitly referenced).  
     *Reference: `Assets/GraphFlowSandbox/Runtime/Scaffold.GraphFlow.M0.asmdef`.*

2. **Runner type**
   - `Runner = typeof(T)` where **`T` subclasses `GraphRunner`** (from your runtime graph package, e.g. M0).
   - `T` must be **non-abstract** and visible to the declaring assembly.

3. **Assembly attribute**
   - Exactly one logical configuration per runner per assembly is typical; **multiple** `[GraphPackage]` attributes are allowed **only** if you intentionally run multiple independent packages from the same consumer assembly (ExecPlan: multiple runners).

4. **Required metadata for production codegen**
   - **`Extension`** — stable, unique per package where possible; no leading `.`.
   - **`AssetMenu`** — user-facing create path.
   - **`Convention`** — matches how payloads expose ports.
   - **`RegistryNamespace`** — namespace for generated types (e.g. `MyGame.Effects.Generated`).

5. **Mode 2 (optional)**  
   If binding existing hierarchies without marker interfaces, set **`CommandBase`**, **`EntryBase`**, and execution helpers (**`DispatcherBase`**, etc.) per ExecPlan v2. Mode 1 uses `IGraphEntry<T>` / `IGraphAction<T>` on payloads only.

6. **Language version**
   - Emitted sources must compile with the consumer asmdef language version (Unity often defaults to **C# 9**). The generator avoids C# 10-only syntax unless you upgrade the consumer.

7. **Sync built DLLs**
   - After changing the generator or attributes projects, run **`dotnet build`** on the generator solution and copy DLLs into `Assets/Packages/com.scaffold.graphflow/` (see `Generators/Scaffold.GraphFlow/sync-unity-dlls.ps1`).

### Common mistakes

- Forgetting the **GUID reference** to **`Scaffold.GraphFlow.PackageGenerator.dll`** → generator never runs on that assembly.
- Putting `[GraphPackage]` on a **type** instead of the **assembly** → not discovered.
- **`Runner`** left default / null → invalid configuration for emission.

### Fast checks

- Unity **Console** free of generator-related compile errors.
- IDE **Source Generators / Analyzers** node shows **`GraphPackageGeneratorBootstrap`** (or subsequent emitted files) for the consumer assembly.
- Commenting out `[assembly: GraphPackage]` removes generated output → confirms the pipeline.

## How to Use

1. Add **`com.scaffold.graphflow`** (embedded under `Assets/Packages/`) to the project.
2. Wire **Attributes** + **PackageGenerator** references on the consumer asmdef (see checklist).
3. Add a small C# file with **`[assembly: GraphPackage(Runner = typeof(...), ...)]`**.
4. Recompile; inspect generated sources under the analyzer node for your assembly.
5. When runtime/editor packages are on M1 parity, delete hand-written duplicates and keep a single source of truth in payloads + attribute.

## Examples

### Minimal assembly declaration (M0 smoke)

Place in `Assets/GraphFlowSandbox/Runtime/AssemblyInfo.cs` (same folder as `Scaffold.GraphFlow.M0.asmdef`):

```csharp
#pragma warning disable SCA0009
using Scaffold.GraphFlow;
using Scaffold.GraphFlow.M0.Smoke;

[assembly: GraphPackage(
    Runner = typeof(MySmokeRunner),
    Extension = "gfmsmoke",
    AssetMenu = "GraphFlow M0 Smoke Graph",
    Convention = PortConvention.AllFieldsIn,
    RegistryNamespace = "Scaffold.GraphFlow.M0.Generated")]
#pragma warning restore SCA0009
```

### Guard: missing runner

```csharp
// Invalid — Runner must be set to a concrete GraphRunner subclass.
[assembly: GraphPackage(
    Extension = "x",
    AssetMenu = "Broken",
    Convention = PortConvention.AllFieldsIn,
    RegistryNamespace = "Broken.Generated")]
```

## Best Practices

- Keep **one ExecPlan** (`Plans/GraphFlow/ExecPlan-v2.md`) as the semantic source of truth for behaviors not duplicated here.
- Prefer **one `[GraphPackage]` per runner** per assembly unless you have a deliberate multi-runner assembly.
- Use **stable `Extension`** values; changing them invalidates existing asset paths and importers.
- After generator changes, always **sync DLLs** and let Unity reimport.
- Keep generated code **C# 9 safe** until Unity / asmdef LangVersion is raised project-wide.

## Anti-Patterns

- Declaring `[GraphPackage]` without referencing the **generator** on that asmdef.
- Using **file-scoped namespaces** or other C# 10+-only patterns in emitted code while consumers compile as **C# 9**.
- Mixing two runners’ payloads in one package declaration without reading ExecPlan multi-binding rules (future **`EFG008`**).

## Testing

- **Build:** `dotnet build Generators/Scaffold.GraphFlow.PackageGenerator/Scaffold.GraphFlow.PackageGenerator.csproj -c Release`
- **Sync:** `Generators/Scaffold.GraphFlow/sync-unity-dlls.ps1`
- **Unity:** Compile `Scaffold.GraphFlow.M0` (or your consumer); inspect generated `GraphPackageGeneratorBootstrap` (bootstrap) and forthcoming trio/registry sources.

## AI Agent Context

- Generator entry: **`GraphPackageIncrementalGenerator`** in `Generators/Scaffold.GraphFlow.PackageGenerator/`.
- Attribute metadata name: **`Scaffold.GraphFlow.GraphPackageAttribute`** (full metadata name for Roslyn).
- Unity wiring mirrors **`com.scaffold.autopacker`** (Roslyn analyzer `.meta` labels, explicit asmdef GUID reference).
- Full vertical slice reference: **`Assets/GraphFlowSandbox/`**.

## Related

- [ExecPlan v2](../../../Plans/GraphFlow/ExecPlan-v2.md)
- [GraphFlow M0 smoke](../../GraphFlowSandbox/)

## Changelog

### 0.1.0

- Initial embedded package: attributes DLL, PackageGenerator DLL, README with graph package requirements.
