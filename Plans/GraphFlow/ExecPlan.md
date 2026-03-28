# GraphFlow — Visual graph authoring, bake pipeline, and awaitable runtime execution

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

After this work, a contributor can author a **flow graph** in the Unity Editor using **Unity Graph Toolkit** (a package that provides editor-only graph windows, nodes, and serialized graph assets). The graph describes **control flow** between nodes that are defined in C# as **definition types** (the single authoring point for port shape). A **bake step** turns the edited graph into a **runtime graph asset** that does not depend on Graph Toolkit assemblies in player builds. Game code can **start** a graph run from one of several **named entry points**, obtain an **awaitable** execution, and observe **middleware** firing **before and after** each node with access to the **per-node instance** object and the active **`Flow`**. Middleware may **start an entirely separate graph run** (same or different `RuntimeGraph`), not only **nested subgraph** nodes; those starts should receive a **`Flow`** argument (typically a **new child flow** whose **parent** is the current flow) so cancellation, diagnostics, and blackboard policy stay coherent. A run **ends** at a **return** node (with or without a value) or when **no successor** exists on the active flow path.

Someone can verify success by running the repository’s EditMode tests (see `Concrete Steps`) and by stepping through a small sample graph: multiple entry names, a branch, middleware that awaits, **middleware that awaits a separate graph run** while passing **child `Flow`**, nested graph invocation from a **node** with **child flow** linked to **parent**, and termination via return versus dead-end.

## Progress

- [ ] (2026-03-28 00:00Z) Authored initial ExecPlan for GraphFlow (Graph Toolkit, generator, ScriptedImporter, runtime executor).
- [ ] Add package dependency and minimal Graph Toolkit graph window proof (open asset, place nodes).
- [ ] Implement handcrafted base types (`Flow`, connection placeholders, definition base, middleware contracts).
- [ ] Implement Roslyn generator: discover definitions, emit editor node shells and runtime registration glue.
- [ ] Implement ScriptedImporter bake: graph asset to runtime graph asset; preserve connection map.
- [ ] Implement `GraphRunner`: multi-entry, awaitable traversal, return and no-next termination, middleware pipeline.
- [ ] Implement nested graph run and **middleware-started** graph runs (child `Flow` holds parent; `Flow` passed through `GraphRunner` / middleware APIs); document re-entrancy expectations.
- [ ] Add EditMode tests and sample graph assets; document module in `Docs/` when implementation stabilizes.
- [ ] Optional final pass: definition-level customization (attributes or partial methods copied or invoked by generator).

## Surprises & Discoveries

- Observation: (none yet)
  Evidence: (none yet)

## Decision Log

- Decision: Treat a **definition** type as the sole authoring point for **port direction and data types**, using distinct field types such as `InputConnection<T>` and `OutputConnection<T>` so the source generator does not need per-field direction attributes.
  Rationale: Direction is expressed in the type system; reduces attribute noise and keeps editor, bake, and runtime shapes aligned.
  Date/Author: 2026-03-28 / planning

- Decision: Use a **dedicated graph source generator** for node-related emission; do not extend **AutoPacker** to own Graph Toolkit editor output or connection topology.
  Rationale: AutoPacker is optimized for pack/unpack structs and unmanaged rules; graph ports and bake IR are a different product. Optional later use of AutoPacker only for unrelated wire payloads.
  Date/Author: 2026-03-28 / planning

- Decision: Implement asset baking with **ScriptedImporter** (as in Unity Graph Toolkit sample patterns): the importer reads the **serialized graph authoring asset** and produces or updates a **runtime graph** asset consumed by the player.
  Rationale: Keeps bake on import deterministic and avoids manual “Bake” menu drift for standard workflows.
  Date/Author: 2026-03-28 / planning

- Decision: **Per-traversal instance** data is separate from the **immutable baked definition**; middleware and node execution receive the **instance** for the current step so concurrent flows do not share mutable node state.
  Rationale: Addresses races when multiple runs or async middleware interact with the same graph asset.
  Date/Author: 2026-03-28 / planning

- Decision: When one graph starts another, create a **new child `Flow`** (or equivalent context object) that **references the parent** flow for cancellation, diagnostics, or variable inheritance policy.
  Rationale: First instinct matches clear lifecycle boundaries; exact inheritance rules for blackboard data are finalized during runner implementation.
  Date/Author: 2026-03-28 / planning

- Decision: **Middleware** may start a **full new graph run** (any graph asset, any entry), not only runs triggered by a dedicated **nested-graph node**. The public **start/run API** and middleware **context** must expose the current **`Flow`** and accept a **`Flow`** (or parent link) when starting additional runs so callers can correlate **child** runs with the **active** run and propagate cancellation consistently.
  Rationale: Effects and interceptors often spawn parallel or follow-on flows; reusing the same runner entry point with an explicit **parent `Flow`** avoids ad-hoc globals and matches nested-subgraph semantics.
  Date/Author: 2026-03-28 / planning

## Outcomes & Retrospective

(To be filled as milestones complete.)

## Context and Orientation

**Scaffold** is a modular Unity project under `Assets/Scripts/` with analyzers in `Analyzers/` and optional Roslyn generators under `Generators/`. See `Architecture.md` for layout. This feature adds a new cohesive area (suggested location: under `Assets/Scripts/` as a new module folder such as `GraphFlow/` with separate **Runtime** and **Editor** assembly definitions so **Graph Toolkit** references exist only in Editor assemblies).

**Unity Graph Toolkit** means the Unity package that provides **editor-only** APIs (`Unity.GraphToolkit.Editor`, graph assets, `Graph` / `Node` / `INode` / `IPort`). Graphs authored with it are **not** player-safe by default; the **baked runtime graph** must be plain Unity-serializable data without those references.

**ScriptedImporter** means a Unity `ScriptedImporter` subclass registered for a file extension: when Unity imports that file, the importer builds **sub-assets** or a **main asset** (typically `ScriptableObject`) that holds the **runtime graph**.

**Definition** means a C# type (usually `partial class`) that declares **which inputs and outputs exist** using generic field types. The **source generator** reads that type and emits **editor graph nodes** (subclasses of Graph Toolkit `Node` with matching ports) and **runtime glue** (identifiers, registration, or accessors) so implementers only maintain the definition and hand-written **execution** code.

**Flow** (or **graph context**) means the per-run object that tracks **current node**, **cancellation**, **parent flow** (when this run was started from another run or from middleware), and any **blackboard** or **entry name** used when multiple exits exist from an entry point. **Middleware** means an ordered list of hooks invoked **before** and **after** each node’s core execution, each **awaitable**, receiving the **definition handle**, **instance**, and the **active `Flow`**, so another system can **start a separate graph run** (with a **new child `Flow`** whose parent is that active flow), **await** it, and only then allow the runner to proceed—same pattern as a **nested-graph node**, but triggered outside node logic.

**Entry point with multiple flow exits** means a single graph **entry node type** (or entry metadata) that exposes **named outbound flow ports** (for example `Run` versus `Validate`). Code selects which **named exit** to follow when starting or when resolving the first step.

## Plan of Work

The work proceeds in milestones that each leave a **verifiable** artifact. Prefer additive changes: new assemblies and tests first, then integration with the Graph Toolkit package and importer registration. Keep **player** assemblies free of `UnityEditor` and Graph Toolkit references, and keep **generated** code in `partial` types so hand-written logic stays in non-generated files.

Milestone zero establishes **contracts and handcrafted bases**. Introduce a **Runtime** assembly containing `Flow`, cancellation or timeout hooks if needed, middleware delegate or interface types, and **handwritten** generic types `InputConnection<T>` and `OutputConnection<T>` (or equivalent names) with **no** generator involvement so base types stay stable and readable. Add a **definition base class** carrying a single type-level attribute such as `[GraphNodeDefinition]` for generator discovery. Support nodes **without** a per-run instance (for example `VoidInstance` or a default type argument) and both **sync** and **async** execution by choosing either two bases or one base whose execute method returns **`ValueTask`** and completes synchronously for trivial nodes. Document how a hand-written **partial** implements execution and receives **instance**. Acceptance is compilation plus a minimal test that invokes middleware in order without Graph Toolkit.

Milestone one adds the **Roslyn source generator** in a new project under `Generators/`, mirroring AutoPacker layout. The generator must **not** reference Graph Toolkit; it scans definitions, validates allowed field types, and emits **partial** companions with node kind ids and **port metadata** for the runner. Do not generate the connection generic types themselves. Acceptance is a compiling sample definition with a generated `*.g.cs` file and optional snapshot tests for stable output.

Milestone two adds an **Editor** assembly referencing Graph Toolkit and extends the generator with an emission pass that outputs **`Node`** subclasses whose ports match definition fields, including **flow** ports under a fixed convention (for example one flow in and one flow out, or multiple named flow outs on entry nodes). Port **names** must match definition field names for reliable baking. Acceptance is a graph asset whose generated nodes show correct ports in the graph window.

Milestone three implements the **ScriptedImporter** for the authoring file extension. On import, walk editor nodes and `IPort` connectivity (`GetNodes`, port enumerators, `GetConnectedPorts`), map each placed node to a **stable definition id** from generation, and write or update a **runtime graph** `ScriptableObject` with nodes, edges, entry bindings (including **named** exits such as Run versus Validate), and return nodes. Failed validation should log clearly and avoid writing a misleading runtime asset. Acceptance is deterministic reimport and an EditMode test that proves bake output for a minimal graph (using a checked-in asset or an editor-test construction path).

Milestone four implements **GraphRunner** with an **awaitable** public API that always accepts or produces a **`Flow`** instance (for example `RunAsync(RuntimeGraph graph, string entryName, Flow parentFlow, …)` or an overload that creates a **root** flow when `parentFlow` is null). Prefer **`UnityEngine.Awaitable`** if the project’s Unity version supports it consistently; otherwise use **`ValueTask`** or **`Task`** and record the choice in the Decision Log. The **middleware context** must include the **active `Flow`** so hooks can call the same **start/run** API to launch **another** graph and **await** it. Each step allocates or resets the **per-node instance**, awaits **Before** middleware, awaits **node execution**, then awaits **After** middleware; it follows **flow** edges including **multiple** outputs from an entry; it stops on a **return** node (with or without a value) or when **no** successor exists. Acceptance is EditMode coverage for multi-entry, dead-end, return variants, async middleware ordering, and **middleware that starts and awaits a child graph run** using a **child `Flow`** linked to the current flow.

Milestone five covers **nested graphs from nodes** and aligns it with the **same** child-flow rule as middleware: a **subgraph** or **invoke-graph** node creates a **child `Flow`**, runs the target graph **awaitably** via the shared runner API, then resumes the parent. Pick and test one **blackboard** policy (for example isolated versus inherited). Acceptance is a test that proves ordering, that the parent does not finish before the child, and that **middleware-spawned** and **node-nested** runs both use the same **parent/child `Flow`** contract.

Milestone six is **optional customization**: attributes or partial hooks that the generator copies into editor metadata or bake records so samples do not require hand-editing generated files.

## Milestones summary (index)

Milestone 0 is runtime contracts and bases. Milestone 1 is generator metadata emission. Milestone 2 is Graph Toolkit editor nodes. Milestone 3 is ScriptedImporter bake. Milestone 4 is awaitable `GraphRunner` with middleware. Milestone 5 is nested flows from **nodes**, sharing the same **Flow** contract as middleware-started runs. Milestone 6 is optional definition-level customization.

## Concrete Steps

Work from the repository root (`/workspace` or your local clone).

- Add and configure Unity package **`com.unity.graphtoolkit`** (version pinned in `Packages/manifest.json`) when Milestone 2 begins.
- After substantive code changes, run EditMode tests using the project script described in `Architecture.md`:

    powershell -File .agents/scripts/run-editmode-tests.ps1

  Working directory: repository root. Expect the script to exit successfully; note passed/failed counts in `Surprises & Discoveries` if failures occur.

- When analyzers are required to be clean per repository policy, run:

    powershell -File .agents/scripts/check-analyzers.ps1

  If the environment lacks PowerShell, use the documented alternative in prior ExecPlans (for example direct `dotnet build` for analyzer projects) and record evidence.

- For generator-only validation without full Unity:

    dotnet build Generators/<GraphGeneratorProject>/<Project>.csproj -c Release

## Validation and Acceptance

Acceptance for the **overall** initiative: at least one **sample** runtime graph asset exists in `Assets/` (or test resources), **two named entry paths** from one entry node behave differently in a test assertion, **middleware** logs or counters prove **Before**/**After** ordering around node execution, **return** and **no-next** both terminate with distinguishable outcomes, **nested** graph run from a **node** completes with parent **awaiting** child, and **middleware** can **start and await** a **separate** graph run with a **child `Flow`** whose **parent** is the active run. All new EditMode tests pass under `run-editmode-tests.ps1`.

For each milestone, add or extend tests **before** claiming completion; if a bug fix appears in a milestone, include a regression test that failed before the fix and passes after, per `PLANS.md`.

## Idempotence and Recovery

Reimporting the same authoring asset should overwrite or deterministically update the baked runtime asset. Generator output should be fully regenerable: deleting `*.g.cs` files and rebuilding recreates them. Feature flags or `#if UNITY_EDITOR` guard editor-only code paths so player builds never reference editor assemblies.

## Artifacts and Notes

Indented examples below are illustrative only; final names may differ if the Decision Log is updated.

    // Handwritten definition (consumer assembly)
    [GraphNodeDefinition]
    public partial class AddNumbersDefinition : GraphNodeDefinitionBase<AddNumbersInstance>
    {
        public InputConnection<int> A;
        public InputConnection<int> B;
        public OutputConnection<int> Sum;
        public FlowInput In;
        public FlowOutput Out;
    }

    // Generated: Editor node with ports A, B, Sum, In, Out
    // Generated: Runtime registration and port metadata
    // Handwritten partial: Execute(AddNumbersInstance instance, Flow flow) { ... }

## Interfaces and Dependencies

**Unity:** Graph Toolkit package (`com.unity.graphtoolkit`), `ScriptedImporter` API, `ScriptableObject` for runtime graph storage, optional `Awaitable` for async graph runs.

**Scaffold:** New **Runtime** `.asmdef` for `Flow`, middleware, runner, runtime graph data; new **Editor** `.asmdef` referencing Graph Toolkit and the Runtime assembly for importer and editor nodes; new **Generator** project referenced from consumer assemblies via `Analyzer` / source generator metadata (match existing AutoPacker wiring pattern in the repo).

**Minimum types (names indicative):** `IFlow` or `Flow` (with **`Parent`** and optional **root** discovery for cancellation), `GraphRunResult`, `GraphRunner.RunAsync(RuntimeGraph graph, string entryName, Flow parentFlow, CancellationToken ct)` (or equivalent overloads so **game code** and **middleware** share one entry point), `MiddlewareContext` exposing **current `Flow`**, **definition**, **instance**, and **node id**, middleware `Func<MiddlewareContext, Func<ValueTask>, ValueTask>` or interface with `Before`/`After`, `RuntimeGraph` (ScriptableObject), `InputConnection<>`, `OutputConnection<>`, `FlowInput` / `FlowOutput` (or named flow port types), `[GraphNodeDefinition]` on definition types.

## Implementation Plan Index

- (Optional) `Plans/GraphFlow/milestones/ExecPlan-Milestone-1.md` through subsequent milestones—add when a milestone needs a standalone document.

## Change History

- 2026-03-28: Initial ExecPlan authored from design discussion (definition-first ports, dedicated generator, ScriptedImporter bake, awaitable runner, middleware, nested flows, multi-entry).

- 2026-03-28: Clarified that **middleware** may start **separate** graph runs (not only nested subgraph nodes); **`Flow`** must be passed through **runner** and **middleware** APIs; child flows reference **parent** for both cases.
