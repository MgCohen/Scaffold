# GraphFlow — Visual graph authoring, bake pipeline, and awaitable runtime execution

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

After this work, a contributor can author a **flow graph** in the Unity Editor using **Unity Graph Toolkit** (a package that provides editor-only graph windows, nodes, and serialized graph assets). The graph describes **control flow** between nodes that are defined in C# as **definition types** (the single authoring point for port shape). A **bake step** turns the edited graph into a **runtime graph asset** that does not depend on Graph Toolkit assemblies in player builds. Game code can **start** a graph run from one of several **typed entry points** (each entry is a **CLR type**, which can carry **payload fields** or be an empty marker), **query** which entry types exist on the **runtime graph** asset, obtain an **awaitable** execution, and observe **middleware** firing **before and after** each node with access to the **per-node instance** object and the active **`Flow`**. Starting a run may pass **only** a **`CancellationToken`**, an existing **`Flow`**, or both—creating a **root** `Flow` is optional when the overload allows it. Middleware may **start an entirely separate graph run** (same or different `RuntimeGraph`), not only **nested subgraph** nodes; those starts should receive a **`Flow`** argument (typically a **new child flow** whose **parent** is the current flow) so cancellation, diagnostics, and blackboard policy stay coherent. Cross-cutting behavior is **wired during an explicit setup phase** (composition) so other graphs or systems can attach **before/after** logic **without singletons**. An optional **`MonoBehaviour`** (or small **host** type) can wrap runner construction for scene-based workflows. A run **ends** at a **return** node (with or without a value) or when **no successor** exists on the active flow path.

Someone can verify success by running the repository’s EditMode tests (see `Concrete Steps`) and by stepping through a small sample graph: **two distinct entry types** (for example `QuestRunEntry` versus `QuestValidateEntry`) with different first steps, a branch, middleware registered at **setup** time, **middleware that awaits a separate graph run** via an **instance** method on **`GraphRunner`** (for example `runner.RunChildGraphAsync<TEntry>(...)`), nested graph invocation from a **node** with **child flow** linked to **parent**, and termination via return versus dead-end.

## Progress

- [ ] (2026-03-28 00:00Z) Authored initial ExecPlan for GraphFlow (Graph Toolkit, generator, ScriptedImporter, runtime executor).
- [ ] Add package dependency and minimal Graph Toolkit graph window proof (open asset, place nodes).
- [ ] Implement handcrafted base types (`Flow`, connection placeholders, definition base, middleware contracts).
- [ ] Implement Roslyn generator: discover definitions, emit editor node shells and runtime registration glue.
- [ ] Implement ScriptedImporter bake: graph asset to runtime graph asset; preserve connection map.
- [ ] Implement `GraphRunner`: **typed** multi-entry, optional `Flow`/`ct` overloads, awaitable traversal, return and no-next termination, middleware pipeline; **entry discovery** on `RuntimeGraph`.
- [ ] Implement **setup / composition** API so middleware and cross-graph hooks register without singletons; optional **`GraphFlowHost`** `MonoBehaviour` (or equivalent) wrapping runner boilerplate.
- [ ] Implement nested graph run and **middleware-started** graph runs (child `Flow` holds parent; `Flow` passed through `GraphRunner` / middleware APIs); document re-entrancy expectations.
- [ ] Add EditMode tests and sample graph assets; document module in `Docs/` when implementation stabilizes.
- [ ] Optional final pass: definition-level customization (attributes or partial methods copied or invoked by generator).
- [ ] Sample scenario validation: keyboard + **GameStartEntry**, reactive **before Add** child graph, **multiply in place** on **`AddNumbersInstance`**, **logger** prints **4** (see **Expected result (trace)**).

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

- Decision: **Per-traversal instance** data is separate from the **immutable graph definition** (the C# **definition** type and serialized **runtime graph** topology); middleware and node execution receive the **instance** for the current step so concurrent flows do not share mutable node state.
  Rationale: Addresses races when multiple runs or async middleware interact with the same graph asset.
  Date/Author: 2026-03-28 / planning

- Decision: When one graph starts another, create a **new child `Flow`** (or equivalent context object) that **references the parent** flow for cancellation, diagnostics, or variable inheritance policy.
  Rationale: First instinct matches clear lifecycle boundaries; exact inheritance rules for blackboard data are finalized during runner implementation.
  Date/Author: 2026-03-28 / planning

- Decision: **Middleware** may start a **full new graph run** (any graph asset, any entry), not only runs triggered by a dedicated **nested-graph node**. The public **start/run API** and middleware **context** must expose the current **`Flow`** and accept a **`Flow`** (or parent link) when starting additional runs so callers can correlate **child** runs with the **active** run and propagate cancellation consistently.
  Rationale: Effects and interceptors often spawn parallel or follow-on flows; reusing the same runner entry point with an explicit **parent `Flow`** avoids ad-hoc globals and matches nested-subgraph semantics.
  Date/Author: 2026-03-28 / planning

- Decision: **Node execution** is expressed as a **`public virtual` or `protected abstract`** method on the definition type hierarchy. When the runner must invoke through a **non-generic** contract (`object` instance), the **public** entry that accepts **`object`** is **`sealed`** and performs the **cast**, then forwards to a **`protected`** method with the **correct instance type** (or the generic `TInstance` method on `GraphNodeDefinitionBase<TInstance>`). Hand-written code only implements the **typed** method.
  Rationale: Keeps casting in one generated or base-class location; implementations stay strongly typed.
  Date/Author: 2026-03-28 / planning

- Decision: **Instance wiring** (populate inputs from edges, blackboard, or entry payload) is emitted as **`partial` instance methods on the same definition type** in the generated partial, **not** as **`static`** helpers, **not** as separate factory types, mirroring how **AutoPacker** keeps `Pack` on the annotated partial class as instance API.
  Rationale: Single type as the anchor; wiring runs through the **definition instance** held by the registry (or created per run policy), keeping everything **in line or in instance**.
  Date/Author: 2026-03-28 / planning

- Decision: **`GraphRunner.RunAsync`** supports **minimal call sites**: overloads that accept **only** `CancellationToken` (runner creates an internal **root** `Flow` with **`new Flow(ct)`** or an injected **`IFlowFactory`**) and overloads that pass an explicit **`Flow`** when the caller needs shared blackboard or parentage. **Child** graph runs use an **instance** method **`RunChildGraphAsync`** on **`GraphRunner`**—**not** a captured lambda pattern as the primary recommended style.
  Rationale: Matches “optional new flow” and avoids middleware examples that rely on lambdas closing over runner state.
  Date/Author: 2026-03-28 / planning

- Decision: **Avoid `static` members** in GraphFlow runtime and setup types **except** for **extension method** classes (`static` methods on `this` first parameter). Factories, composition, child runs, and flow creation use **instance** methods, **`new`**, or **injected services**—no `static` helpers “for convenience” outside extension types.
  Rationale: Keeps testability and explicit dependency flow; extensions remain the only sanctioned static surface.
  Date/Author: 2026-03-28 / planning

- Decision: **Entry points** are identified by **CLR types** (for example `QuestRunEntry`, `QuestValidateEntry`), not by **string** names. Each type may be an **empty marker** or a **struct/class with payload fields** serialized or passed when starting a run. The **`RuntimeGraph`** asset exposes a **list or read-only view of entry types** (plus metadata) discoverable at runtime. The importer stores a **stable type id** (assembly-qualified or project-defined) per entry node.
  Rationale: Typed entries enable **payload**, compile-time checks, and rename safety versus magic strings.
  Date/Author: 2026-03-28 / planning

- Decision: **Middleware and cross-graph reactions** attach during a **setup** or **composition** phase: a **`GraphFlowBuilder`**, **`IGraphMiddlewareContributor`**, or explicit registration object passed from the game bootstrap (DI container, scene host, or test fixture). No **singleton** registry is required; the **same composed `GraphRunner`** (or environment object) is passed to code that needs to run graphs. Multiple graphs can register contributors when the host builds the runner once per scope.
  Rationale: Lets “another graph” or subsystem participate in **before/after** by receiving the builder or runner config at startup, not by locating a global service.
  Date/Author: 2026-03-28 / planning

- Decision: Provide an **optional** presentation-layer **`MonoBehaviour`** (for example **`GraphFlowHost`**) that holds **serialized `RuntimeGraph` references**, performs **setup** (build runner, register middleware), and exposes **awaitable** entry methods for **scenes** or **prefabs**. The **core runner** remains usable without `MonoBehaviour` for pure C# tests and services.
  Rationale: Reduces boilerplate in Unity workflows while keeping a **non-Unity** “brain” (`GraphRunner` + `Flow`) as the real center of execution.
  Date/Author: 2026-03-28 / planning

## Outcomes & Retrospective

(To be filled as milestones complete.)

## Context and Orientation

**Scaffold** is a modular Unity project under `Assets/Scripts/` with analyzers in `Analyzers/` and optional Roslyn generators under `Generators/`. See `Architecture.md` for layout. This feature adds a new cohesive area (suggested location: under `Assets/Scripts/` as a new module folder such as `GraphFlow/` with separate **Runtime** and **Editor** assembly definitions so **Graph Toolkit** references exist only in Editor assemblies).

**Unity Graph Toolkit** means the Unity package that provides **editor-only** APIs (`Unity.GraphToolkit.Editor`, graph assets, `Graph` / `Node` / `INode` / `IPort`). Graphs authored with it are **not** player-safe by default; the **baked runtime graph** must be plain Unity-serializable data without those references.

**ScriptedImporter** means a Unity `ScriptedImporter` subclass registered for a file extension: when Unity imports that file, the importer builds **sub-assets** or a **main asset** (typically `ScriptableObject`) that holds the **runtime graph**.

**Definition** means a C# type (usually `partial class`) that declares **which inputs and outputs exist** using generic field types. The **source generator** reads that type and emits **editor graph nodes** (subclasses of Graph Toolkit `Node` with matching ports) and **runtime glue** (identifiers, registration, or accessors) so implementers only maintain the definition and hand-written **execution** code.

**Flow** (or **graph context**) means the per-run object that tracks **current node**, **cancellation**, **parent flow** (when this run was started from another run or from middleware), and any **blackboard** or **entry payload** supplied when starting from a **typed entry**. **Middleware** means an ordered list of hooks invoked **before** and **after** each node’s core execution, each **awaitable**, receiving the **definition handle**, **instance**, and the **active `Flow`**, so another system can **start a separate graph run** (with a **new child `Flow`** whose parent is that active flow), **await** it, and only then allow the runner to proceed—same pattern as a **nested-graph node**, but triggered outside node logic.

**Typed entry point** means a **CLR type** recorded in the **runtime graph** asset by the importer (for example `QuestRunEntry` or `QuestValidateEntry`) mapped to a specific **entry node** in the graph and its **outbound flow ports**. Starting a run specifies **`typeof(TEntry)`** or a **generic parameter** `RunAsync<TEntry>(...)` and may pass an **instance of `TEntry`** as payload. **Multiple entry types** on one graph replace **string** entry names.

**Setup phase** means the moment **before** any run (bootstrap, scene load, or test arrange) when code constructs a **`GraphRunner`** (or **`GraphFlowEnvironment`**) and registers **middleware contributors**, **child-graph policies**, and any **cross-graph** listeners by **explicit** hand-off of a builder or options object—no implicit global singleton.

## Plan of Work

The work proceeds in milestones that each leave a **verifiable** artifact. Prefer additive changes: new assemblies and tests first, then integration with the Graph Toolkit package and importer registration. Keep **player** assemblies free of `UnityEditor` and Graph Toolkit references, and keep **generated** code in `partial` types so hand-written logic stays in non-generated files.

Milestone zero establishes **contracts and handcrafted bases**. Introduce a **Runtime** assembly containing `Flow`, cancellation or timeout hooks if needed, middleware delegate or interface types, **`GraphFlowBuilder`** (or equivalent) for **setup-time** registration, and **handwritten** generic types `InputConnection<T>` and `OutputConnection<T>` (or equivalent names) with **no** generator involvement so base types stay stable and readable. Add a **definition base class** carrying a single type-level attribute such as `[GraphNodeDefinition]` for generator discovery, with **`protected abstract`** (or **`protected virtual`**) **typed** execution and a **`sealed public`** (or **`sealed` bridge**) path that accepts **`object`** and casts to **`TInstance`** when required by the registry. Support nodes **without** a per-run instance (for example `VoidInstance` or a default type argument) and both **sync** and **async** execution by choosing either two bases or one base whose execute method returns **`ValueTask`** and completes synchronously for trivial nodes. Document how a hand-written **partial** overrides only the **typed** execute method. Acceptance is compilation plus a minimal test that builds a runner via **setup** and invokes middleware in order without Graph Toolkit.

Milestone one adds the **Roslyn source generator** in a new project under `Generators/`, mirroring AutoPacker layout. The generator must **not** reference Graph Toolkit; it scans definitions, validates allowed field types, and emits **partial** companions on the **same definition type** with node kind ids, **port metadata**, **`sealed`** **object**-to-**`TInstance`** execute bridge if applicable, and **instance** wiring methods (**`WireInstanceInputs`** / **`PublishInstanceOutputs`** or similar) as **instance** methods **on the definition partial**—**not** `static`, not separate factory types. Do not generate the connection generic types themselves. Acceptance is a compiling sample definition with a generated `*.g.cs` file and optional snapshot tests for stable output.

Milestone two adds an **Editor** assembly referencing Graph Toolkit and extends the generator with an emission pass that outputs **`Node`** subclasses whose ports match definition fields, including **flow** ports under a fixed convention (for example one flow in and one flow out, or **multiple flow outs** on **typed entry** nodes such as `QuestRunEntry` versus `QuestValidateEntry`). Port **names** must match definition field names for reliable baking. **Entry node** types in the editor must reference or imply the **CLR entry type** so bake can record **stable type ids**. Acceptance is a graph asset whose generated nodes show correct ports in the graph window.

Milestone three implements the **ScriptedImporter** for the authoring file extension. On import, walk editor nodes and `IPort` connectivity (`GetNodes`, port enumerators, `GetConnectedPorts`), map each placed node to a **stable definition id** from generation, and write or update a **runtime graph** `ScriptableObject` with nodes, edges, **typed entry** table (**entry CLR type id** to **node id** and **per-port successor map**), and return nodes. Expose **`GetEntryPoints()`** (or **`EntryPoints`** property) returning the list of **entry types** stored in the asset. Failed validation should log clearly and avoid writing a misleading runtime asset. Acceptance is deterministic reimport and an EditMode test that proves importer output for a minimal graph (using a checked-in asset or an editor-test construction path).

Milestone four implements **GraphRunner** with an **awaitable** public API: overloads that accept **`RuntimeGraph` + `CancellationToken` only** (runner creates an internal **root** `Flow` via **`new Flow(ct)`** or an injected **`IFlowFactory`**), overloads that accept an explicit **`Flow`**, and overloads that accept **`TEntry` payload** for **typed** starts (for example `RunAsync<TEntry>(RuntimeGraph graph, TEntry entry, Flow flow = null, CancellationToken ct = default)` with **`flow` optional**). Provide an **instance** method **`RunChildGraphAsync<TEntry>(RuntimeGraph graph, Flow parent, CancellationToken ct)`** on **`GraphRunner`** for **child** runs **without** recommending lambda-based middleware patterns. Prefer **`UnityEngine.Awaitable`** if the project’s Unity version supports it consistently; otherwise use **`ValueTask`** or **`Task`** and record the choice in the Decision Log. The **middleware context** must include the **active `Flow`** and, where needed, a **`GraphRunner`** reference so middleware can call **`RunChildGraphAsync`** without statics. Each step allocates or resets the **per-node instance**, calls **generated instance wiring** on the **definition** instance from **`INodeExecutorRegistry`**, awaits **Before** middleware, awaits **node execution** (via **typed** method), awaits **After** middleware; it follows **flow** edges including **multiple** outputs from an **entry type**; it stops on a **return** node (with or without a value) or when **no** successor exists. Acceptance is EditMode coverage for **two entry types**, dead-end, return variants, async middleware ordering, **setup-time** middleware registration, and **middleware that starts and awaits a child graph run** via **`runner.RunChildGraphAsync<TEntry>(...)`**.

Milestone five covers **nested graphs from nodes** and aligns it with the **same** child-flow rule as middleware: a **subgraph** or **invoke-graph** node creates a **child `Flow`**, runs the target graph **awaitably** via the shared runner API, then resumes the parent. Pick and test one **blackboard** policy (for example isolated versus inherited). Acceptance is a test that proves ordering, that the parent does not finish before the child, and that **middleware-spawned** and **node-nested** runs both use the same **parent/child `Flow`** contract.

Milestone six is **optional customization**: attributes or partial hooks that the generator copies into editor metadata or bake records so samples do not require hand-editing generated files.

Milestone seven (optional, can merge with 0/4): add **`GraphFlowHost`** `MonoBehaviour` (or thin wrapper) that serializes **`RuntimeGraph`**, runs **setup** in **`Awake`/`OnEnable`**, exposes **`RunAsync`**-shaped methods to **MonoBehaviour** callers, and forwards to the composed **`GraphRunner`**.

## Milestones summary (index)

Milestone 0 is runtime contracts, bases, **setup** composition API, and **typed execute** pattern. Milestone 1 is generator emission on **definition partial** (metadata, **wiring**, **sealed** bridge). Milestone 2 is Graph Toolkit editor nodes and **typed** entry nodes. Milestone 3 is ScriptedImporter bake and **entry discovery**. Milestone 4 is awaitable **`GraphRunner`** with optional **`Flow`**, **instance** **`RunChildGraphAsync`**, and middleware. Milestone 5 is nested flows from **nodes**. Milestone 6 is optional definition-level customization. Milestone 7 is optional **`MonoBehaviour`** host.

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

Acceptance for the **overall** initiative: at least one **sample** runtime graph asset exists in `Assets/` (or test resources), **`RuntimeGraph`** exposes **at least two distinct entry types** that route to different first steps, **`RunAsync` with only `ct`** works (implicit root **`Flow`**) and **`RunAsync` with explicit `Flow`** works, **middleware** registered at **setup** proves **Before**/**After** ordering, **return** and **no-next** both terminate with distinguishable outcomes, **nested** graph run from a **node** completes with parent **awaiting** child, and **middleware** starts a **child** run via **`runner.RunChildGraphAsync<TEntry>(...)`** (instance method, **non-lambda**). All new EditMode tests pass under `run-editmode-tests.ps1`.

For each milestone, add or extend tests **before** claiming completion; if a bug fix appears in a milestone, include a regression test that failed before the fix and passes after, per `PLANS.md`.

## Idempotence and Recovery

Reimporting the same authoring asset should overwrite or deterministically update the baked runtime asset. Generator output should be fully regenerable: deleting `*.g.cs` files and rebuilding recreates them. Feature flags or `#if UNITY_EDITOR` guard editor-only code paths so player builds never reference editor assemblies.

## Artifacts and Notes

The **Snippets and API** section below is **indicative**. Exact type names, namespaces, and whether `Awaitable` versus `ValueTask` is used follow implementation choices recorded in the **Decision Log**.

## Snippets and API (indicative)

### Whole “Add” node example (definition + hand-written execution)

Use **one** hand-written `partial class` for **ports, flow pins, and execution**—do **not** split “values” and “logic” across two partials. The **generated** `partial` adds wiring and any **sealed** bridge the registry needs. **Execution** is a **`protected override`** of a **typed** method on **`GraphNodeDefinitionBase<TInstance>`**; the **base** exposes a **`public sealed`** entry that accepts **`object`** and **casts** to **`TInstance`** before calling the **protected** method.

```csharp
// Hand-crafted base (Runtime) — illustrative
public abstract class GraphNodeDefinitionBase<TInstance> : IGraphNodeDefinition
    where TInstance : class, new()
{
    public sealed async ValueTask ExecuteAsync(object instance, Flow flow, CancellationToken ct)
    {
        await ExecuteAsync((TInstance)instance, flow, ct);
    }

    protected abstract ValueTask ExecuteAsync(TInstance instance, Flow flow, CancellationToken ct);
}

// Hand-written — single partial: ports + flow + ExecuteAsync in one type
[GraphNodeDefinition]
public partial class AddNumbersDefinition : GraphNodeDefinitionBase<AddNumbersInstance>
{
    public InputConnection<int> A;
    public InputConnection<int> B;
    public OutputConnection<int> Sum;
    public FlowInput In;
    public FlowOutput Out;

    protected override async ValueTask ExecuteAsync(AddNumbersInstance instance, Flow flow, CancellationToken ct)
    {
        instance.Sum = instance.A + instance.B;
        await default(ValueTask);
    }
}
```

### How the generated instance type may look

The generator emits a **mutable per-visit** type with **one field per data port** on the definition (inputs and outputs as plain values the runner and **`ExecuteAsync`** read/write). **Connection** fields from the definition do not appear on the instance.

```csharp
// Generated — AddNumbersInstance.g.cs (illustrative)
namespace MyGame.GraphFlow.Nodes
{
    public sealed class AddNumbersInstance
    {
        public int A;
        public int B;
        public int Sum;
    }
}
```

Nodes **without** per-run state may use a generated empty instance, `GraphNodeDefinitionBase<VoidInstance>`, or a shared `VoidInstance` singleton policy—finalize in Milestone 0.

### How the generated editor node may look

Emitted in an **Editor** assembly: a Graph Toolkit `Node` subclass with **data** ports and **flow** ports matching the definition field names.

```csharp
// Generated — Editor assembly — AddNumbersDefinitionEditorNode.g.cs (illustrative)
using System;
using Unity.GraphToolkit.Editor;

[Serializable]
internal sealed class AddNumbersDefinitionEditorNode : Node
{
    protected override void OnDefinePorts(IPortDefinitionContext context)
    {
        context.AddInputPort<int>("A").Build();
        context.AddInputPort<int>("B").Build();
        context.AddOutputPort<int>("Sum").Build();
        context.AddInputPort("In").Build();   // flow pin — type may be connection-only per Graph Toolkit
        context.AddOutputPort("Out").Build();
    }
}
```

Flow pins may use **untyped** `AddInputPort` / `AddOutputPort` when the toolkit should treat them as **control-flow only**; exact pattern follows Graph Toolkit version and team convention.

### How the instance is built and filled

The **runner** allocates or pools **`AddNumbersInstance`**, then calls **generated methods on the same `partial` definition type** (AutoPacker-style: logic lives next to the type), for example **`WireInstanceInputs`** / **`PublishInstanceOutputs`**, **not** a separate factory type.

1. For the **current node id**, allocate or pool an **`AddNumbersInstance`** (or reset fields).
2. The runner resolves the **`IGraphNodeDefinition`** instance from **`INodeExecutorRegistry`** for that **definition type id** and invokes **`definition.WireInstanceInputs(instance, flow, stepContext)`** (name indicative)—**generated** as an **instance** method in **`AddNumbersDefinition.*.g.cs`** from field metadata.
3. Call **Before** middleware.
4. Call **`definition.ExecuteAsync(instance, flow, ct)`** (uses **sealed** **`object`** entry → **protected typed** override).
5. Call **After** middleware.
6. Call generated **`PublishInstanceOutputs`** if needed, then advance using **runtime** **flow** edges from the graph asset.

```csharp
// Generated — AddNumbersDefinition.Wiring.g.cs (illustrative) — instance methods, not static
public partial class AddNumbersDefinition
{
    public void WireInstanceInputs(
        AddNumbersInstance target,
        Flow flow,
        GraphStepContext ctx)
    {
        target.A = ctx.ReadInput<int>(nameof(A));
        target.B = ctx.ReadInput<int>(nameof(B));
    }

    public void PublishInstanceOutputs(AddNumbersInstance source, GraphStepContext ctx)
    {
        ctx.WriteOutput(nameof(Sum), source.Sum);
    }
}
```

**`GraphStepContext`** is an indicative façade the runner fills so generated code stays short (backed by edge tables, prior node cache, and **typed entry** payload).

### Other important types (illustrative)

```csharp
// Root context for one graph run; child runs use Parent = current flow. No static factory here — use `new Flow(ct)` or inject IFlowFactory.
public sealed class Flow
{
    public Flow(CancellationToken cancellation) { }

    public Flow Parent { get; }
    public CancellationToken Cancellation { get; }
    public GraphBlackboard Blackboard { get; }
    public NodeId CurrentNodeId { get; internal set; }

    public Flow CreateChild() { throw new NotImplementedException(); }
}

public sealed class GraphBlackboard
{
    // string/object or typed bags — decide in implementation
}

public readonly struct NodeId : IEquatable<NodeId>
{
    public Guid Value { get; }
}

// Runtime graph asset produced by ScriptedImporter — summary shape (names use Runtime*, not Baked*)
public sealed class RuntimeGraph : ScriptableObject
{
    public IReadOnlyList<RuntimeGraphNode> Nodes { get; }
    public IReadOnlyList<RuntimeGraphEdge> Edges { get; }
    /// <summary>CLR types that can start this graph (marker or payload types).</summary>
    public IReadOnlyList<Type> EntryPointTypes { get; }

    public bool TryGetEntry(Type entryType, out RuntimeGraphEntry entry) { throw new NotImplementedException(); }
}

public sealed class RuntimeGraphNode
{
    public NodeId Id { get; }
    public string DefinitionTypeId { get; } // stable id from generator
}

public sealed class RuntimeGraphEdge
{
    public NodeId FromNode { get; }
    public string FromPort { get; }
    public NodeId ToNode { get; }
    public string ToPort { get; }
}

/// <summary>One typed entry: which node is the entry, and outbound flow port → first node.</summary>
public sealed class RuntimeGraphEntry
{
    public Type EntryClrType { get; }       // typeof(QuestRunEntry), etc.
    public NodeId EntryNodeId { get; }
    public IReadOnlyDictionary<string, NodeId> FlowExitToNextNode { get; } // flow port name → successor
}

public enum MiddlewarePhase { Before, After }

public readonly struct MiddlewareContext
{
    public GraphRunner Runner { get; }     // for RunChildGraphAsync without statics
    public Flow Flow { get; }
    public MiddlewarePhase Phase { get; }
    public NodeId NodeId { get; }
    public Type DefinitionType { get; }
    public object Instance { get; } // actual AddNumbersInstance, etc.
}

public delegate ValueTask GraphMiddleware(MiddlewareContext ctx, Func<ValueTask> next);

// Prefer class-based middleware (no lambda) for child-graph and DI-friendly setup.
public interface IGraphMiddleware
{
    ValueTask InvokeAsync(MiddlewareContext ctx, Func<ValueTask> next);
}

public sealed class GraphRunner
{
    public GraphRunner(IEnumerable<GraphMiddleware> middleware, INodeExecutorRegistry registry) { }

    /// <summary>Creates an internal root <see cref="Flow"/> for this run (e.g. <c>new Flow(ct)</c>).</summary>
    public ValueTask<GraphRunResult> RunAsync<TEntry>(RuntimeGraph graph, CancellationToken ct = default) { }

    /// <summary><paramref name="flow"/> null creates a root flow; otherwise runs under the given flow.</summary>
    public ValueTask<GraphRunResult> RunAsync<TEntry>(RuntimeGraph graph, Flow flow, CancellationToken ct = default) { }

    /// <summary>Start with typed payload (struct or class); <paramref name="flow"/> null creates a root flow.</summary>
    public ValueTask<GraphRunResult> RunAsync<TEntry>(
        RuntimeGraph graph,
        in TEntry entryPayload,
        Flow flow = null,
        CancellationToken ct = default) { }

    /// <summary>Child run under <paramref name="parent"/>; instance method — use from middleware via <see cref="MiddlewareContext.Runner"/>.</summary>
    public ValueTask<GraphRunResult> RunChildGraphAsync<TEntry>(
        RuntimeGraph graph,
        Flow parent,
        CancellationToken ct = default) { }
}

public readonly struct GraphRunResult
{
    public bool CompletedAtReturn { get; }
    public bool StoppedNoNext { get; }
    public object ReturnValue { get; }
}
```

`INodeExecutorRegistry` maps **`DefinitionTypeId`** from the **runtime graph** asset to an **`IGraphNodeDefinition`** instance (stateless **definition** singleton per kind) that exposes **`ExecuteAsync(object, Flow, CancellationToken)`**—the **sealed** path that casts to **`TInstance`**.

### Setup phase (no singleton): compose runner and middleware

```csharp
// Bootstrap / DI / test arrange — build once per scope; instance-based composition, no static factory
public sealed class SampleGraphComposition
{
    public GraphRunner CreateRunner(GraphRunnerDependencies deps)
    {
        var builder = new GraphFlowBuilder(deps.Registry);
        builder.UseMiddleware(new LoggingMiddleware(deps.Log));
        builder.UseMiddleware(new CrossGraphMiddleware(deps.InnerRunner, deps.OtherGraph));
        return builder.Build();
    }
}
```

Contributors implement **`IGraphMiddleware`** or register **`GraphMiddleware`** delegates supplied by **whoever owns setup** (container, scene host, test). **`GraphFlowBuilder`** is indicative; the exact type name is fixed during Milestone 0.

### Extension methods (only sanctioned `static` surface)

```csharp
// All other statics avoided; extension classes may use static methods for ergonomics.
public static class GraphRunnerExtensions
{
    public static ValueTask<GraphRunResult> RunAsync<TEntry>(this GraphRunner runner, RuntimeGraph graph)
        => runner.RunAsync<TEntry>(graph, CancellationToken.None);
}
```

### How to trigger the flow from code

```csharp
// Game or service — minimal call: only graph + ct (implicit root Flow)
public class QuestService
{
    readonly GraphRunner runner;
    [SerializeField] RuntimeGraph questGraph;

    public async ValueTask StartQuestRunAsync(CancellationToken ct)
    {
        GraphRunResult result = await runner.RunAsync<QuestRunEntry>(questGraph, ct);
        if (result.CompletedAtReturn)
            ApplyReward(result.ReturnValue);
    }

    public async ValueTask ValidateAsync(CancellationToken ct)
    {
        var payload = new QuestValidateEntry { LevelId = currentLevel };
        await runner.RunAsync(questGraph, payload, flow: null, ct);
    }
}
```

Discover entries without running:

```csharp
IReadOnlyList<Type> entries = questGraph.EntryPointTypes;
bool canValidate = questGraph.TryGetEntry(typeof(QuestValidateEntry), out var runtimeEntry);
```

### Child graph from middleware (no lambda, no static runner API)

Implement **`IGraphMiddleware`** as a **class**. Use **`ctx.Runner.RunChildGraphAsync`** (same **`GraphRunner`** instance the parent run uses):

```csharp
public sealed class SpawnChildGraphMiddleware : IGraphMiddleware
{
    readonly RuntimeGraph childGraph;

    public SpawnChildGraphMiddleware(RuntimeGraph childGraph)
    {
        this.childGraph = childGraph;
    }

    public async ValueTask InvokeAsync(MiddlewareContext ctx, Func<ValueTask> next)
    {
        await ctx.Runner.RunChildGraphAsync<ChildEntry>(childGraph, ctx.Flow, ctx.Flow.Cancellation);
        await next();
    }
}
```

### Optional MonoBehaviour “central brain”

```csharp
// Presentation-facing thin wrapper — optional; core logic stays in GraphRunner + Flow
public sealed class GraphFlowHost : MonoBehaviour
{
    [SerializeField] RuntimeGraph mainGraph;
    [SerializeField] RuntimeGraph reactiveGraph;

    GraphRunner runner;

    void Awake()
    {
        var composition = new SampleGraphComposition();
        var deps = new GraphRunnerDependencies(
            registry: /* resolve or field */,
            reactiveGraph: reactiveGraph,
            targetBeforeAdd: typeof(AddNumbersDefinition));
        runner = composition.CreateRunner(deps);
    }

    public ValueTask<GraphRunResult> RunAsync<TEntry>(CancellationToken ct = default)
        => runner.RunAsync<TEntry>(mainGraph, ct);

    async void Start()
    {
        await RunAsync<GameStartEntry>(destroyCancellationToken);
    }
}
```

Scene code references **`GraphFlowHost`** when convenient; headless tests use **`GraphRunner`** directly. **`SampleGraphComposition.CreateRunner`** registers **`GraphReactiveDispatchMiddleware`** (and any other global middleware) so **every** run sees the same pipeline.

### How to let “another graph” react to before/after

At **setup**, pass the **`GraphFlowBuilder`** (or a **`GraphFlowEnvironment`** options object) into the module that owns the second graph so it can call **`builder.UseMiddleware(new MyCrossGraphMiddleware(...))`**. **`MyCrossGraphMiddleware`** holds **`GraphRunner`**, **`RuntimeGraph`**, and any **policies** by **constructor injection**—no static service locator. If the second graph must **observe** runs of the first without being middleware on the same runner, register a **shared event bus** or **`IObserver<GraphRunEvent>`** **also** at setup and have middleware on the first runner **publish** to that channel (still **wired in composition**, not a singleton unless the project already uses one globally).

### Main classes (inventory for the end-to-end sample flow)

These names are **indicative**; group them mentally as **entry markers**, **definitions**, **runtime asset**, **runner**, **flow state**, **composition**, and **presentation**.

- **`GameStartEntry`**: empty marker type (or struct with no fields)—**generic “start”** entry for the **main** graph when the scene loads or a host calls **`RunAsync<GameStartEntry>`**.
- **`KeyPressedEntry`**: empty marker type—entry for **keyboard**-triggered runs; code calls **`RunAsync<KeyPressedEntry>`** when a key goes down.
- **`AddNumbersDefinition`** / **`AddNumbersInstance`**: as already documented—the **main** graph computes **Sum** from **A** and **B**.
- **`ReactiveBeforeAddEntry`**: payload type holding **`AddNumbersInstance Add`** (or a **non-null reference** slot)—**typed entry** for the **reactive** graph so the child run receives the **same instance** the parent is about to execute.
- **`MultiplyAddInputsDefinition`**: node that **mutates** the **`AddNumbersInstance`** referenced from **`Flow.ReactivePayload`** (or equivalent)—doubles **A** and **B** in place (**paste back** is **by reference**, no copy).
- **`LogObjectDefinition`** / **`LogObjectInstance`**: **`InputConnection<object>`** (or **`string`**)—**`Debug.Log`** in **`ExecuteAsync`**.
- **`RuntimeGraph`** (**main** and **reactive** assets): two **`ScriptableObject`** assets produced by the importer—**reactive** graph referenced by **`GraphReactiveDispatchMiddleware`** (or host) at setup.
- **`Flow`**: carries **`ReactivePayload`** (or **`ReactiveRunState`**) for **child** runs so reactive nodes read the **parent’s current node instance** without statics.
- **`GraphRunner`**: runs **main** and **child** graphs via **`RunAsync`** / **`RunChildGraphAsync`**.
- **`GraphReactiveDispatchMiddleware`**: **`IGraphMiddleware`** instance registered at **setup**—on **Before** (and optionally **After**) for a **configured target definition** (e.g. **Add**), starts **`RunChildGraphAsync<ReactiveBeforeAddEntry>`** with **`new ReactiveBeforeAddEntry(addInstance)`**, **awaits**, then **`next()`**.
- **`SampleGraphComposition`**: builds **`GraphRunner`** with **global** middleware list including **`GraphReactiveDispatchMiddleware`**.
- **`GraphFlowHost`** / **`KeyboardGraphTrigger`**: **`MonoBehaviour`**—**host** runs **`GameStartEntry`**; **keyboard** component calls **`RunAsync<KeyPressedEntry>`** on the same runner (or host forwards).

### Reactive hook: timing and target type from editor dropdowns

**Ideal:** A **subscription** or **hook** node sits on the **main** graph (or a sidecar **graph settings** asset). Its serialized fields include **`NodeHookTiming`** (**Before** / **After**), **target definition** (dropdown of registered **`[GraphNodeDefinition]`** types), and **which reactive graph asset** to run. The **ScriptedImporter** emits **`RuntimeGraphReactiveHook`** records into the **main** **`RuntimeGraph`** (list of hooks). **`GraphReactiveDispatchMiddleware`** loads that list (or receives it at construction) and matches **`MiddlewareContext.DefinitionType`** and **before vs after** phase.

**Graph Toolkit note:** Dropdowns are **serialized fields** on a **custom `Node`** or **`ScriptableObject`**; a **custom property drawer** or **ObjectField** can list types from a **generated registry**. If the toolkit makes **complex** per-node serialization awkward in v1, use **fallback A**: **separate CLR entry types** per hook (**`ReactiveBeforeAddEntry`**, **`ReactiveAfterAddEntry`**) and **separate reactive graph assets**—no dropdown, only **wiring** which graph to run from **composition** (middleware constructor args). **Fallback B:** **one** payload type **`ReactiveHookEntry`** with **`NodeHookTiming Timing`** and **`string TargetDefinitionTypeId`** set only from **code** or a **ScriptableObject** hook list **outside** the graph canvas.

**Recommendation for the sample:** Implement **middleware-driven** hooks first (**constructor-injected** target type + timing + reactive graph reference); add **importer-emitted** **`RuntimeGraphReactiveHook`** when editor UX is ready.

### Snippets: keyboard entry, generic start, multiplier, logger, reactive payload, middleware

**Entry marker types (no fields):**

```csharp
public sealed class GameStartEntry { }
public sealed class KeyPressedEntry { }
```

**Reactive entry payload (same `AddNumbersInstance` reference as parent step):**

```csharp
public sealed class ReactiveBeforeAddEntry
{
    public ReactiveBeforeAddEntry(AddNumbersInstance add) => Add = add;
    public AddNumbersInstance Add { get; }
}
```

**`Flow` extension for reactive child runs (illustrative):**

```csharp
public sealed class Flow
{
    // ... existing members ...
    /// <summary>Set by runner when starting a reactive child run; cleared when child completes.</summary>
    public object ReactivePayload { get; set; }
}
```

**Multiplier: mutates the add instance in place (uses `ReactivePayload`; `VoidInstance` or empty instance type):**

```csharp
[GraphNodeDefinition]
public partial class MultiplyAddInputsDefinition : GraphNodeDefinitionBase<VoidInstance>
{
    public FlowInput In;
    public FlowOutput Out;

    protected override ValueTask ExecuteAsync(VoidInstance _, Flow flow, CancellationToken ct)
    {
        var add = (AddNumbersInstance)flow.ReactivePayload;
        add.A *= 2;
        add.B *= 2;
        return default;
    }
}
```

**Logger node:**

```csharp
[GraphNodeDefinition]
public partial class LogObjectDefinition : GraphNodeDefinitionBase<LogObjectInstance>
{
    public InputConnection<object> Message;
    public FlowInput In;
    public FlowOutput Out;

    protected override ValueTask ExecuteAsync(LogObjectInstance instance, Flow flow, CancellationToken ct)
    {
        UnityEngine.Debug.Log(instance.Message);
        return default;
    }
}

// Generated instance includes: public object Message;
```

**Setup: one middleware instance dispatches reactive graph before Add (injected at composition time):**

```csharp
public sealed class GraphReactiveDispatchMiddleware : IGraphMiddleware
{
    readonly GraphRunner runner;
    readonly RuntimeGraph reactiveGraph;
    readonly Type targetDefinitionType;
    readonly bool runBeforeNode;

    public GraphReactiveDispatchMiddleware(
        GraphRunner runner,
        RuntimeGraph reactiveGraph,
        Type targetDefinitionType,
        bool runBeforeNode = true)
    {
        this.runner = runner;
        this.reactiveGraph = reactiveGraph;
        this.targetDefinitionType = targetDefinitionType;
        this.runBeforeNode = runBeforeNode;
    }

    public async ValueTask InvokeAsync(MiddlewareContext ctx, Func<ValueTask> next)
    {
        bool isBeforePhase = ctx.Phase == MiddlewarePhase.Before;
        if (isBeforePhase == runBeforeNode && ctx.DefinitionType == targetDefinitionType
            && ctx.Instance is AddNumbersInstance add)
        {
            var child = ctx.Flow.CreateChild();
            child.ReactivePayload = add;
            var entry = new ReactiveBeforeAddEntry(add);
            await runner.RunAsync(reactiveGraph, entry, child, ctx.Flow.Cancellation);
        }

        await next();
    }
}
```

**Note:** **`MiddlewareContext`** should expose **`MiddlewarePhase BeforeOrAfter`** (or split delegates) so one class can support **after** hooks without duplicating the runner. The generator or base runner implements that.

**Keyboard trigger (`MonoBehaviour`):**

```csharp
public sealed class KeyboardGraphTrigger : MonoBehaviour
{
    [SerializeField] GraphFlowHost host;
    [SerializeField] KeyCode key = KeyCode.Space;

    void Update()
    {
        if (UnityEngine.Input.GetKeyDown(key))
            _ = host.RunAsync<KeyPressedEntry>(destroyCancellationToken);
    }
}
```

**Initial values:** set **`A`** and **`B`** to **1** on the **main** graph via **blackboard** seed in **`GameStartEntry`** handling, **constant nodes** in the graph asset, or **first** nodes before **Add**—the sample assumes after **wire** inputs the **Add** instance holds **1** and **1** before the **Before** middleware runs.

### Expected result (trace for the sample)

The following trace assumes **setup** registered **`GraphReactiveDispatchMiddleware`** on the **same** **`GraphRunner`** that runs the **main** graph, targeting **`typeof(AddNumbersDefinition)`**, **before** phase, with **`reactiveGraph`** containing **entry** **`ReactiveBeforeAddEntry`** → **MultiplyAddInputs** → **return** (or end). **Main** graph: **entry** (**`GameStartEntry`** or **`KeyPressedEntry`**) → **Add** → **Log** ( **`Message`** fed from **Sum**). **`Flow.ReactivePayload`** is set on the **child** flow before the reactive **`RunAsync`** so the multiplier mutates the **same** **`AddNumbersInstance`** the parent **`WireInstanceInputs`** already filled with **1** and **1**.

1. **Main** graph starts from **click** (**`KeyPressedEntry`**) or **scene start** (**`GameStartEntry`**).
2. **Runner** wires **Add** instance: **A = 1**, **B = 1**.
3. **Before Add** middleware runs: **`GraphReactiveDispatchMiddleware`** matches **Add** node, **Before** phase.
4. Middleware builds **`ReactiveBeforeAddEntry(addInstance)`**, creates **child** **`Flow`**, sets **`child.ReactivePayload = addInstance`**, **`await runner.RunAsync(reactiveGraph, entry, child, ct)`**.
5. **Reactive** graph **entry** runs; **MultiplyAddInputs** **`ExecuteAsync`** reads **`flow.ReactivePayload`**, sets **A = 2**, **B = 2** on the **same** object.
6. **Reactive** graph completes; middleware **`await`** returns; **main** **`next()`** continues.
7. **Add** **`ExecuteAsync`**: **Sum = 2 + 2 = 4**.
8. **Logger** logs **4** ( **`Message`** wired from **Sum** on the **main** graph).

If **After** reactive hooks are needed, register a **second** middleware instance with **`runBeforeNode: false`** or extend **`GraphReactiveDispatchMiddleware`** to read **hook records** from **`RuntimeGraph`**.

## Interfaces and Dependencies

**Unity:** Graph Toolkit package (`com.unity.graphtoolkit`), `ScriptedImporter` API, `ScriptableObject` for runtime graph storage, optional `Awaitable` for async graph runs.

**Scaffold:** New **Runtime** `.asmdef` for `Flow`, middleware, runner, runtime graph data; new **Editor** `.asmdef` referencing Graph Toolkit and the Runtime assembly for importer and editor nodes; new **Generator** project referenced from consumer assemblies via `Analyzer` / source generator metadata (match existing AutoPacker wiring pattern in the repo).

**Minimum types (names indicative):** `Flow` (**`ReactivePayload`**, **`new Flow(ct)`**, **`Parent`**, **`CreateChild`**), `MiddlewarePhase`, `GraphRunResult`, **`GraphRunner`**, **`RunAsync` / `RunChildGraphAsync`**, **`RuntimeGraph`** (**`RuntimeGraphNode`**, **`RuntimeGraphEdge`**, **`RuntimeGraphEntry`**, **`RuntimeGraphReactiveHook`** when importer supports editor hooks), **`MiddlewareContext`** (**`Runner`**, **`Phase`**), **`GraphReactiveDispatchMiddleware`**, entry markers **`GameStartEntry`**, **`KeyPressedEntry`**, **`ReactiveBeforeAddEntry`**, **`GraphFlowBuilder`**, **`IGraphMiddleware`**, **`IGraphNodeDefinition`**, **`INodeExecutorRegistry`**, **`VoidInstance`**, **`KeyboardGraphTrigger`**, **`GraphFlowHost`**, **extension-only** `static` classes. See **Snippets and API** and **Main classes (inventory)**.

## Implementation Plan Index

- (Optional) `Plans/GraphFlow/milestones/ExecPlan-Milestone-1.md` through subsequent milestones—add when a milestone needs a standalone document.

## Change History

- 2026-03-28: Initial ExecPlan authored from design discussion (definition-first ports, dedicated generator, ScriptedImporter bake, awaitable runner, middleware, nested flows, multi-entry).

- 2026-03-28: Clarified that **middleware** may start **separate** graph runs (not only nested subgraph nodes); **`Flow`** must be passed through **runner** and **middleware** APIs; child flows reference **parent** for both cases.

- 2026-03-28: Expanded **Snippets and API**: full Add node example, generated instance and editor node shapes, instance fill pipeline, illustrative core types (`Flow`, `RuntimeGraph`, `MiddlewareContext`, `GraphRunner`), code trigger and middleware child-run sample.

- 2026-03-28: **Typed entry points** (replace string names), **`EntryPointTypes` / `TryGetEntry`**, **optional `Flow`** (`ct`-only overload), **`RunChildGraphAsync`** without lambda, **sealed public + protected typed** execute, **wiring on definition partial**, **`GraphFlowBuilder` setup**, optional **`GraphFlowHost`** `MonoBehaviour`.

- 2026-03-28: **No statics** except **extension methods**; **`RunChildGraphAsync`** and **`Flow`** creation are **instance** / **`new`**; **`Baked*`** renamed to **`RuntimeGraphNode`**, **`RuntimeGraphEdge`**, **`RuntimeGraphEntry`**; **Add** definition in **one** hand-written partial; **`MiddlewareContext.Runner`** for child runs; wiring methods are **instance** on definition partial.

- 2026-03-28: Added **main class inventory**, **reactive hook** design (dropdown vs fallbacks), **snippets** (**keyboard**, **start**, **multiplier**, **logger**, **`GraphReactiveDispatchMiddleware`**, **`Flow.ReactivePayload`**), **`MiddlewarePhase`** on **`MiddlewareContext`**, and **expected trace** (1+1 → before-add reactive → 2+2 → 4).
