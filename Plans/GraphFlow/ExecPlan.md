# GraphFlow — Visual graph authoring, bake pipeline, and awaitable runtime execution

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

After this work, a contributor can author a **flow graph** in the Unity Editor using **Unity Graph Toolkit** (a package that provides editor-only graph windows, nodes, and serialized graph assets). The graph describes **control flow** between nodes that are defined in C# as **definition types** (the single authoring point for port shape). A **bake step** turns the edited graph into a **runtime graph asset** that does not depend on Graph Toolkit assemblies in player builds. Game code can **start** a graph run from one of several **typed entry points** (each entry is a **CLR type**, which can carry **payload fields** or be an empty marker), **query** which entry types exist on the **runtime graph** asset, obtain an **awaitable** execution, and observe **middleware** firing **before and after** each node with access to the **per-node instance** object and the active **`Flow`**. Starting a run may pass **only** a **`CancellationToken`**, an existing **`Flow`**, or both—creating a **root** `Flow` is optional when the overload allows it. Middleware may **start an entirely separate graph run** (same or different `RuntimeGraph`), not only **nested subgraph** nodes; those starts should receive a **`Flow`** argument (typically a **new child flow** whose **parent** is the current flow) so cancellation, diagnostics, and blackboard policy stay coherent. Cross-cutting behavior uses a **central service** that owns the **shared middleware list**, **discovers** each **graph** (and its **runner**), and runs **`graph.Initialize(middlewares)`** so every **node definition** can run **`Initialize`** for **entry registration** (keyboard, etc.). **Reactive** hooks are **generic middleware** driven by **serialized hook records** on **`RuntimeGraph`** (dropdowns on editor nodes), not bespoke composition per reaction. An optional **`MonoBehaviour`** can wrap **graph** references and participate in discovery. A run **ends** at a **return** node (with or without a value) or when **no successor** exists on the active flow path.

Someone can verify success by running the repository’s EditMode tests (see `Concrete Steps`) and by stepping through a small sample graph: **two distinct entry types** (both **`GraphEntryPoint`** subclasses) with different first steps, **`GraphFlowCentralService.Bootstrap`** calling **`Initialize`** on graphs, **data-driven** **`ReactiveHookMiddleware`**, **`RunChildGraphAsync`** on **`GraphRunner`**, nested graph invocation from a **node** with **child flow** linked to **parent**, and termination via return versus dead-end.

## Progress

- [ ] (2026-03-28 00:00Z) Authored initial ExecPlan for GraphFlow (Graph Toolkit, generator, ScriptedImporter, runtime executor).
- [ ] Add package dependency and minimal Graph Toolkit graph window proof (open asset, place nodes).
- [ ] Implement handcrafted base types (`Flow`, connection placeholders, definition base, middleware contracts).
- [ ] Implement Roslyn generator: discover definitions, emit editor node shells and runtime registration glue.
- [ ] Implement ScriptedImporter bake: graph asset to runtime graph asset; preserve connection map.
- [ ] Implement `GraphRunner`: **typed** multi-entry, optional `Flow`/`ct` overloads, awaitable traversal, return and no-next termination, middleware pipeline; **entry discovery** on `RuntimeGraph`.
- [ ] Implement **`GraphFlowCentralService`** (name indicative): holds **middleware**, **discovers** graphs, **`graph.Initialize(middlewares)`**; **`IGraphNodeDefinition.Initialize`** / **`RuntimeGraph.Initialize`** pipeline.
- [ ] Optional **`GraphFlowHost`** `MonoBehaviour` for discovery and serialized **`RuntimeGraph`** references.
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

- Decision: **`[GraphNodeDefinition]`** is **optional** when the type **already inherits** from **`GraphNodeDefinitionBase`** (or **`GraphNodeDefinitionBase<TInstance>`**): the source generator discovers definitions by **base type** (and optional attribute for edge cases such as **sealed third-party** subclasses if ever needed).
  Rationale: Avoid redundant metadata; inheritance is the primary marker.
  Date/Author: 2026-03-28 / planning

- Decision: Nodes **without** per-step instance data use a **non-generic** **`GraphNodeDefinitionBase`** ( **`ExecuteAsync(Flow, CancellationToken)`** only)—**not** **`VoidInstance`** and **not** **`GraphNodeDefinitionBase<VoidInstance>`**.
  Rationale: Clearer API and fewer dummy types.
  Date/Author: 2026-03-28 / planning

- Decision: All **entry payload** types inherit **`GraphEntryPoint`** (abstract marker base). **`RuntimeGraph.EntryPointTypes`** (and **`RunAsync<TEntry>`** constraints) filter to **`where TEntry : GraphEntryPoint`** so **non-entry** CLR types cannot be used as graph entries by mistake.
  Rationale: Better filtering and documentation at compile time.
  Date/Author: 2026-03-28 / planning

- Decision: **Keyboard** (and similar) **inputs** do **not** use a separate **`MonoBehaviour`** trigger class: the **wanted key** lives on the **entry payload** (e.g. **`KeyPressedEntry : GraphEntryPoint`** with **`KeyCode WantedKey`**). The **entry node definition** implements **`Initialize`** to either **poll** **`Input.GetKeyDown`** each tick via a **central tick/update service** passed in context, or **register** a callback with that service—**one** definition type, no extra “trigger” type beside the payload.
  Rationale: Keeps input configuration on the graph entry and payload as requested.
  Date/Author: 2026-03-28 / planning

- Decision: **Entry node definitions** expose **`Initialize(GraphInitializationContext context)`** (name indicative) for **registration** (input, timers, services). **Non-entry** definitions may use a **no-op default** or omit override. **Graph-level** **`RuntimeGraph.Initialize`** (or **`IGraphFlowObject.Initialize`**) receives **middleware** from a **central service** and forwards to **each** registered **definition** instance: **`node.Initialize(context)`** or **`node.Initialize(graph)`** where **`graph`** exposes **`GetMiddlewarePipeline()`**, **runner**, etc.
  Rationale: Centralized bootstrap: one middleware list, many graphs/nodes configured consistently.
  Date/Author: 2026-03-28 / planning

- Decision: **Reactive** behavior uses **one generic** **`ReactiveHookMiddleware`** (or equivalent) driven by **`RuntimeGraphReactiveHook`** records produced by the **importer** from **editor** nodes with **dropdowns** (**timing**, **target definition type**, **reactive graph asset**, optional **entry type**). **No** per-reaction **`SampleGraphComposition`** wiring of **`typeof(AddNumbersDefinition)`**—configuration is **data** on the graph, editable **in real time** in the editor.
  Rationale: Matches “generic, anything sent can be reached” and reduces bespoke composition classes.
  Date/Author: 2026-03-28 / planning

- Decision: **Bootstrap** flow: a **central service** holds the **global middleware list**, **discovers** all **`IGraphFlowObject`** (or **`RuntimeGraph` + runner** pairs) in the scene or DI scope, then for **each** graph calls **`graph.Initialize(middlewares)`**, which in turn invokes **`Initialize`** on **each** **node definition** instance (or passes **`graph`** so nodes call **`graph`** helpers). **Each graph** keeps **its own** **`GraphRunner`** instance (or equivalent) while sharing **middleware** **instances** from the center.
  Rationale: Matches the user’s described initialization sequence.
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

- Decision: **Entry points** are **CLR types** that inherit **`GraphEntryPoint`** (for example `QuestRunEntry`, `QuestValidateEntry`, `KeyPressedEntry`), not **string** names. The **`RuntimeGraph`** asset exposes **entry types** discoverable at runtime. The importer stores a **stable type id** per entry node.
  Rationale: **`GraphEntryPoint`** filters **RunAsync** and **discovery**; payloads stay typed.
  Date/Author: 2026-03-28 / planning

- Decision: **Middleware** is registered on a **`GraphFlowCentralService`** (or passed from DI once), then **`Initialize(middlewares)`** on **each** **`IGraphFlowObject`** builds **that graph’s** **`GraphRunner`** with the **same middleware instances**. **Reactive** reactions use **data** on **`RuntimeGraph.ReactiveHooks`** plus **`ReactiveHookMiddleware`**, not a second ad-hoc middleware per reaction.
  Rationale: One global pipeline, many graphs; **data-driven** hooks.
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

**Typed entry point** means a **CLR type** that inherits **`GraphEntryPoint`**, recorded in the **runtime graph** asset by the importer (for example `QuestRunEntry` or `KeyPressedEntry`) mapped to a specific **entry node** in the graph and its **outbound flow ports**. Starting a run uses **`RunAsync<TEntry>(...)`** with **`where TEntry : GraphEntryPoint`** and may pass an **instance of `TEntry`** as payload.

**Setup phase** means **central service** passes **middleware** into **`RuntimeGraph.Initialize(IReadOnlyList<IGraphMiddleware> middleware)`** (indicative), which forwards to **each** **node definition**’s **`Initialize`** so **entry** nodes can **register** or **subscribe** (keyboard polling, etc.). **Each graph** has **its own** **`GraphRunner`**; **middleware** instances are **shared** from the center.

## Plan of Work

The work proceeds in milestones that each leave a **verifiable** artifact. Prefer additive changes: new assemblies and tests first, then integration with the Graph Toolkit package and importer registration. Keep **player** assemblies free of `UnityEditor` and Graph Toolkit references, and keep **generated** code in `partial` types so hand-written logic stays in non-generated files.

Milestone zero establishes **contracts and handcrafted bases**. Introduce **`GraphEntryPoint`** abstract base for all entry payloads; **`GraphNodeDefinitionBase<TInstance>`** for nodes with instance data; **`GraphNodeDefinitionBase`** (non-generic) for nodes **without** instance (**no** **`VoidInstance`**). Optional **`[GraphNodeDefinition]`** only when needed; otherwise **inheritance from a definition base** is sufficient for generator discovery. Add **`Initialize(GraphInitializationContext)`** on definitions (virtual no-op default); **entry** definitions override for **registration** / **polling** setup. Add **`GraphFlowCentralService`** (or equivalent) that **collects middleware**, **finds** all **`IGraphFlowObject`**, calls **`Initialize`**. Acceptance is compilation plus a minimal test: **central** **`Initialize`** runs **before** first **`RunAsync`**.

Milestone one adds the **Roslyn source generator**: discover types by **base class** (and optional attribute), emit **partial** companions with **stable `DefinitionTypeId`**, **port metadata**, **`sealed`** **`ExecuteAsync(object, …)`** bridge for generic base only, **instance** wiring on the **definition partial**, and **`RegisterDefinition` glue** (or **registry partial**) so **`INodeExecutorRegistry`** maps **`RuntimeGraphNode.DefinitionTypeId`** → **`IGraphNodeDefinition`** instance. Do not generate **`InputConnection`/`OutputConnection`** themselves. Acceptance: sample definition compiles with generated `*.g.cs`.

Milestone two adds an **Editor** assembly referencing Graph Toolkit and extends the generator with an emission pass that outputs **`Node`** subclasses whose ports match definition fields, including **flow** ports under a fixed convention (for example one flow in and one flow out, or **multiple flow outs** on **typed entry** nodes such as `QuestRunEntry` versus `QuestValidateEntry`). Port **names** must match definition field names for reliable baking. **Entry node** types in the editor must reference or imply the **CLR entry type** so bake can record **stable type ids**. Acceptance is a graph asset whose generated nodes show correct ports in the graph window.

Milestone three implements the **ScriptedImporter** for the authoring file extension. On import, walk editor nodes and `IPort` connectivity (`GetNodes`, port enumerators, `GetConnectedPorts`), map each placed node to a **stable definition id** from generation, and write or update a **runtime graph** `ScriptableObject` with nodes, edges, **typed entry** table (**entry CLR type id** to **node id** and **per-port successor map**), and return nodes. Expose **`GetEntryPoints()`** (or **`EntryPoints`** property) returning the list of **entry types** stored in the asset. Failed validation should log clearly and avoid writing a misleading runtime asset. Acceptance is deterministic reimport and an EditMode test that proves importer output for a minimal graph (using a checked-in asset or an editor-test construction path).

Milestone four implements **GraphRunner** with **`RunAsync<TEntry>`** where **`TEntry : GraphEntryPoint`**, plus **`RunChildGraphAsync(RuntimeGraph child, GraphEntryPoint entryPayload, Flow childFlow, CancellationToken ct)`** (indicative signature). **Middleware** receives **`MiddlewareContext`** with **`Graph`**, **`DefinitionTypeId`**, **`Runner`**, **`Phase`**. **`ReactiveHookMiddleware`** reads **`ctx.Graph.ReactiveHooks`**. Each step uses **`INodeExecutorRegistry`**, **wiring**, **ExecuteAsync**, **Before**/**After**. Acceptance includes **central** **`Bootstrap`**, **two** **`GraphEntryPoint`** types, and **data-driven** reactive child run.

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
// [GraphNodeDefinition] optional when inheriting GraphNodeDefinitionBase<...>
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

Nodes **without** per-run instance data inherit **`GraphNodeDefinitionBase`** (non-generic) and implement **`ExecuteAsync(Flow flow, CancellationToken ct)`** only—**do not** use **`VoidInstance`**.

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
    /// <summary>Data-driven reactive hooks (importer-filled from editor dropdowns).</summary>
    public IReadOnlyList<RuntimeGraphReactiveHook> ReactiveHooks { get; }

    public bool TryGetEntry(Type entryType, out RuntimeGraphEntry entry) { throw new NotImplementedException(); }
}

public sealed class RuntimeGraphReactiveHook
{
    public MiddlewarePhase Timing { get; }
    public string TargetDefinitionTypeId { get; }
    public RuntimeGraph ReactiveGraph { get; }
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
    public GraphRunner Runner { get; }
    public RuntimeGraph Graph { get; }
    public Flow Flow { get; }
    public MiddlewarePhase Phase { get; }
    public NodeId NodeId { get; }
    public string DefinitionTypeId { get; }
    public Type DefinitionType { get; }
    public object Instance { get; }
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

### Where the definition maps to the runtime graph node

The **chain** is:

1. **Authoring:** Each placed **Graph Toolkit** node is an instance of a **generated editor `Node`** subclass tied to one **C# definition** type (same assembly / discovery as the runtime definition).

2. **Import:** **`ScriptedImporter`** reads each **editor node**, resolves its **definition type**, and stores **`RuntimeGraphNode`** with **`DefinitionTypeId`** (a **stable string** emitted by the source generator into **`DefinitionIds.AddNumbers`** or similar **const** on the definition’s generated partial—**same id** used at compile time and in the asset).

3. **Run:** **`GraphRunner`** loads **`RuntimeGraphNode`** by **`NodeId`**, looks up **`DefinitionTypeId`** in **`INodeExecutorRegistry`**, obtains the **`IGraphNodeDefinition`** **instance** (singleton per definition type, registered at app startup from **generated `RegisterAllDefinitions(registry)`** or **DI**), then allocates the **per-step instance** type if any, calls **generated `WireInstanceInputs`** on that **definition** instance, and calls **`ExecuteAsync`**.

So the **mapping** is **`RuntimeGraphNode.DefinitionTypeId` → registry → `IGraphNodeDefinition`**; the **editor node type** and **C# definition** are aligned by **generator** convention so the **importer** never guesses **CLR** types by string alone—**it reads** serialized **node type** from the asset and maps through a **generated** **type-id table** or **GUID** stable across recompiles.

### Setup phase: central service + per-graph runner

**`GraphFlowBuilder`** may still build a **`GraphRunner`** instance, but **global** **`IGraphMiddleware`** instances (including **`ReactiveHookMiddleware`**) are owned by **`GraphFlowCentralService`**. **`Bootstrap()`** passes the **same middleware list** to **each** **`IGraphFlowObject.Initialize`**. **No** per-reaction **composition** class—**reactive** config lives on **`RuntimeGraph.ReactiveHooks`**.

### Extension methods (only sanctioned `static` surface)

```csharp
public static class GraphRunnerExtensions
{
    public static ValueTask<GraphRunResult> RunAsync<TEntry>(this GraphRunner runner, RuntimeGraph graph)
        where TEntry : GraphEntryPoint
        => runner.RunAsync<TEntry>(graph, CancellationToken.None);
}
```

### How to trigger the flow from code

```csharp
// Game or service — QuestRunEntry : GraphEntryPoint
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

public sealed class QuestRunEntry : GraphEntryPoint { }
public sealed class QuestValidateEntry : GraphEntryPoint { public int LevelId { get; set; } }
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
        await ctx.Runner.RunChildGraphAsync(childGraph, new ChildEntry(), ctx.Flow.CreateChild(), ctx.Flow.Cancellation);
        await next();
    }
}

public sealed class ChildEntry : GraphEntryPoint { }
```

### Optional MonoBehaviour “central brain”

```csharp
// Optional: registers this graph with the central service and runs Bootstrap once (e.g. from a GameBootstrap MonoBehaviour)
public sealed class GraphFlowHost : MonoBehaviour, IGraphFlowObject
{
    [SerializeField] RuntimeGraph mainGraph;
    GraphRunner runner;

    public void Initialize(IReadOnlyList<IGraphMiddleware> middlewares)
    {
        runner = new GraphFlowBuilder(/* registry */).UseMiddlewareRange(middlewares).Build();
        foreach (var def in /* definitions for this asset from registry */)
            def.Initialize(new GraphInitializationContext(runner, mainGraph, tickService: /* ... */));
    }

    public ValueTask<GraphRunResult> RunAsync<TEntry>(CancellationToken ct = default)
        where TEntry : GraphEntryPoint
        => runner.RunAsync<TEntry>(mainGraph, ct);
}
```

Scene code uses **`GraphFlowCentralService`** to **`RegisterGraph`** each **`IGraphFlowObject`**, then **`Bootstrap()`**. Headless tests may call **`Initialize`** directly without **`MonoBehaviour`**.

### How to let “another graph” react to before/after

Prefer **`RuntimeGraph.ReactiveHooks`** on the **main** asset (edited with **dropdowns**) plus **`ReactiveHookMiddleware`** in the **central** middleware list. For **cross-cutting** behavior that is **not** graph-serialized, add **another** **`IGraphMiddleware`** on **`GraphFlowCentralService`** only—**no** per-graph duplicate lists.

### Central bootstrap: middleware, graphs, Initialize

```csharp
// Indicative — central service owns shared middleware; each graph has its own runner
public sealed class GraphFlowCentralService
{
    readonly List<IGraphMiddleware> middlewares = new();
    readonly List<IGraphFlowObject> graphs = new();

    public void RegisterMiddleware(IGraphMiddleware m) => middlewares.Add(m);

    public void RegisterGraph(IGraphFlowObject graph) => graphs.Add(graph);

    public void Bootstrap()
    {
        var shared = middlewares.ToArray();
        foreach (var g in graphs)
            g.Initialize(shared);
    }
}

public interface IGraphFlowObject
{
    void Initialize(IReadOnlyList<IGraphMiddleware> middlewares);
}
```

**`RuntimeGraph`** (or a thin **host** wrapper) implements **`IGraphFlowObject`**: **`Initialize`** builds or rebinds **`GraphRunner`** with **`middlewares`**, then for **each** **`IGraphNodeDefinition`** known from **registry** / **this asset**, calls **`definition.Initialize(context)`** where **`context`** exposes **`IGraphTickService`** (for **`KeyPressedEntry`** polling), **`GraphRunner`**, **blackboard**, etc. **Alternatively** **`node.Initialize(graph)`** only, and **`graph.GetTickService()`** supplies dependencies—pick one pattern and use it consistently.

### Main classes (inventory for the end-to-end sample flow)

- **`GraphEntryPoint`**: abstract base for **all** entry payload types (**`where TEntry : GraphEntryPoint`**).
- **`GameStartEntry : GraphEntryPoint`**: empty or marker—**scene start** / generic start.
- **`KeyPressedEntry : GraphEntryPoint`**: carries **`KeyCode WantedKey`** (and optional **graph id**); **no** separate **`KeyboardGraphTrigger`** class—the **entry node definition** handles **`Initialize`** + **poll** or **register** with **`IGraphTickService`**.
- **`ReactiveChildEntry : GraphEntryPoint`**: single generic payload type for **reactive** subgraph—holds **`object ContextPayload`** (the **current node instance** or a **wrapper**); **reactive graph** reads **`entry.ContextPayload`** and casts. **Optional:** multiple subclasses if you want strong typing without **`object`**.
- **`AddNumbersDefinition`** / **`AddNumbersInstance`**: unchanged pattern (**attribute optional** when inheriting base).
- **`MultiplyAddInputsDefinition`**: **`GraphNodeDefinitionBase`** (non-generic); **`ExecuteAsync`** reads **`flow.ReactivePayload`** (or **`flow.PendingReactiveContext`**) set by **`ReactiveHookMiddleware`** before **`RunChildGraphAsync`**.
- **`LogObjectDefinition`** / **`LogObjectInstance`**: unchanged.
- **`ReactiveHookMiddleware`**: **one** generic **`IGraphMiddleware`**; at **`InvokeAsync`**, reads **`ctx.Graph.ReactiveHooks`** ( **`IReadOnlyList<RuntimeGraphReactiveHook>`** from importer); for each hook where **`hook.TargetDefinitionTypeId`** matches **`ctx.DefinitionType`** and **`hook.Timing`** matches **`ctx.Phase`**, sets **`child.ReactivePayload = ctx.Instance`**, **`await ctx.Runner.RunChildGraphAsync<ReactiveChildEntry>(hook.ReactiveGraph, new ReactiveChildEntry(ctx.Instance), child, ct)`**.
- **`RuntimeGraphReactiveHook`**: serialized **timing**, **targetDefinitionTypeId**, **reactiveGraph** reference, **entryTypeId** (optional)—filled from **editor** node **dropdowns** (types from **generated registry**), **real-time** editable.
- **`GraphRunner`**: **per graph** instance; **`RunChildGraphAsync`** remains **instance** method.
- **`GraphFlowHost`**: **`MonoBehaviour`** implementing **`IGraphFlowObject`** or registering self with **`GraphFlowCentralService`**.

### Snippets: entry base, keyboard payload, non-generic multiplier, generic reactive middleware, Initialize

**Entry base:**

```csharp
public abstract class GraphEntryPoint { }

public sealed class GameStartEntry : GraphEntryPoint { }

public sealed class KeyPressedEntry : GraphEntryPoint
{
    public KeyCode WantedKey { get; set; }
}

public sealed class ReactiveChildEntry : GraphEntryPoint
{
    public ReactiveChildEntry(object contextPayload) => ContextPayload = contextPayload;
    public object ContextPayload { get; }
}
```

**Entry node definition (keyboard self-check via tick service from Initialize):**

```csharp
public partial class KeyPressedEntryNodeDefinition : GraphNodeDefinitionBase<KeyPressedEntryInstance>
{
    public FlowOutput Out;
    IGraphTickService tick;
    GraphRunner runner;
    RuntimeGraph graph;
    KeyPressedEntry templatePayload;

    public override void Initialize(GraphInitializationContext ctx)
    {
        tick = ctx.TickService;
        runner = ctx.Runner;
        graph = ctx.Graph;
        templatePayload = ctx.GetEntryPayloadTemplate<KeyPressedEntry>();
        tick.Register(this, OnTick);
    }

    void OnTick()
    {
        if (UnityEngine.Input.GetKeyDown(templatePayload.WantedKey))
            _ = runner.RunAsync(graph, templatePayload, flow: null, tick.Cancellation);
    }

    protected override ValueTask ExecuteAsync(KeyPressedEntryInstance instance, Flow flow, CancellationToken ct)
        => default; // flow may no-op for pure trigger entries
}
```

(Exact shape of **`KeyPressedEntryInstance`** can be empty generated class or omitted if entry uses non-generic base—**finalize** whether **entry** nodes use **instance** or **non-generic** execute.)

**Multiplier (non-generic base):**

```csharp
public partial class MultiplyAddInputsDefinition : GraphNodeDefinitionBase
{
    public FlowInput In;
    public FlowOutput Out;

    public override ValueTask ExecuteAsync(Flow flow, CancellationToken ct)
    {
        var add = (AddNumbersInstance)flow.ReactivePayload;
        add.A *= 2;
        add.B *= 2;
        return default;
    }
}
```

**Generic reactive middleware (single class, data-driven hooks):**

```csharp
public sealed class ReactiveHookMiddleware : IGraphMiddleware
{
    public async ValueTask InvokeAsync(MiddlewareContext ctx, Func<ValueTask> next)
    {
        foreach (var hook in ctx.Graph.ReactiveHooks)
        {
            if (hook.Timing != ctx.Phase) continue;
            if (hook.TargetDefinitionTypeId != ctx.DefinitionTypeId) continue;

            var child = ctx.Flow.CreateChild();
            child.ReactivePayload = ctx.Instance;
            var entry = new ReactiveChildEntry(ctx.Instance);
            await ctx.Runner.RunChildGraphAsync(hook.ReactiveGraph, entry, child, ctx.Flow.Cancellation);
        }

        await next();
    }
}
```

Use **`DefinitionTypeId`** on **`MiddlewareContext`** (**string** or struct) to avoid **Type** reference mismatches across domains; **`hook.TargetDefinitionTypeId`** comes from the **same** generated **const** as **`RuntimeGraphNode.DefinitionTypeId`**.

**Initial values:** **1** and **1** via constants / blackboard / previous nodes as before.

### Expected result (trace for the sample)

Assume **`GraphFlowCentralService.Bootstrap()`** ran: **`mainGraph.Initialize`** and **`reactiveGraph.Initialize`** received **shared** middleware including **`ReactiveHookMiddleware`**. **Main** **`RuntimeGraph`** contains a **reactive hook** row (**Before**, target **AddNumbers** id, **reactive** graph asset). **Reactive** graph: **entry** **`ReactiveChildEntry`** → **Multiply** → end.

1. **Game start** runs **`RunAsync<GameStartEntry>(mainGraph, ct)`** (or **KeyPressed** entry **`Initialize`** eventually fires **`RunAsync`** with **`KeyPressedEntry`** carrying **WantedKey**).
2. **Runner** wires **Add** instance: **A = 1**, **B = 1**.
3. **Before Add**: **`ReactiveHookMiddleware`** finds hook matching **Add** + **Before**; sets **`ReactivePayload`**, **`RunChildGraphAsync`** with **`ReactiveChildEntry(instance)`**.
4. **Reactive** graph runs **Multiply**; **A**/**B** become **2**/**2** on **same** object.
5. Child completes; **main** continues; **Add** runs **2+2=4**; **Logger** logs **4**.

## Interfaces and Dependencies

**Unity:** Graph Toolkit package (`com.unity.graphtoolkit`), `ScriptedImporter` API, `ScriptableObject` for runtime graph storage, optional `Awaitable` for async graph runs.

**Scaffold:** New **Runtime** `.asmdef` for `Flow`, middleware, runner, runtime graph data; new **Editor** `.asmdef` referencing Graph Toolkit and the Runtime assembly for importer and editor nodes; new **Generator** project referenced from consumer assemblies via `Analyzer` / source generator metadata (match existing AutoPacker wiring pattern in the repo).

**Minimum types (names indicative):** **`GraphEntryPoint`**, **`GraphNodeDefinitionBase`** / **`GraphNodeDefinitionBase<TInstance>`**, optional **`[GraphNodeDefinition]`**, **`GraphInitializationContext`**, **`IGraphTickService`**, **`IGraphFlowObject`**, **`GraphFlowCentralService`**, **`Flow`** (**`ReactivePayload`**), **`MiddlewarePhase`**, **`GraphRunResult`**, **`GraphRunner`** (**`RunAsync<TEntry>`** with **`TEntry : GraphEntryPoint`**), **`RunChildGraphAsync`**, **`RuntimeGraph`** (**`ReactiveHooks`**, **`RuntimeGraphNode`**, **`RuntimeGraphEdge`**, **`RuntimeGraphEntry`**), **`RuntimeGraphReactiveHook`**, **`MiddlewareContext`** (**`Runner`**, **`Graph`**, **`Phase`**, **`DefinitionTypeId`**), **`ReactiveHookMiddleware`**, **`GraphFlowBuilder`**, **`IGraphMiddleware`**, **`IGraphNodeDefinition`**, **`INodeExecutorRegistry`**, **`GraphFlowHost`**, **extension-only** `static` classes. See **Snippets and API**, **Where the definition maps**, **Central bootstrap**.

## Implementation Plan Index

- (Optional) `Plans/GraphFlow/milestones/ExecPlan-Milestone-1.md` through subsequent milestones—add when a milestone needs a standalone document.

## Change History

- 2026-03-28: Initial ExecPlan authored from design discussion (definition-first ports, dedicated generator, ScriptedImporter bake, awaitable runner, middleware, nested flows, multi-entry).

- 2026-03-28: Clarified that **middleware** may start **separate** graph runs (not only nested subgraph nodes); **`Flow`** must be passed through **runner** and **middleware** APIs; child flows reference **parent** for both cases.

- 2026-03-28: Expanded **Snippets and API**: full Add node example, generated instance and editor node shapes, instance fill pipeline, illustrative core types (`Flow`, `RuntimeGraph`, `MiddlewareContext`, `GraphRunner`), code trigger and middleware child-run sample.

- 2026-03-28: **Typed entry points** (replace string names), **`EntryPointTypes` / `TryGetEntry`**, **optional `Flow`** (`ct`-only overload), **`RunChildGraphAsync`** without lambda, **sealed public + protected typed** execute, **wiring on definition partial**, **`GraphFlowBuilder` setup**, optional **`GraphFlowHost`** `MonoBehaviour`.

- 2026-03-28: **No statics** except **extension methods**; **`RunChildGraphAsync`** and **`Flow`** creation are **instance** / **`new`**; **`Baked*`** renamed to **`RuntimeGraphNode`**, **`RuntimeGraphEdge`**, **`RuntimeGraphEntry`**; **Add** definition in **one** hand-written partial; **`MiddlewareContext.Runner`** for child runs; wiring methods are **instance** on definition partial.

- 2026-03-28: Added **main class inventory**, **reactive hook** design (dropdown vs fallbacks), **snippets** (**keyboard**, **start**, **multiplier**, **logger**, **`ReactiveHookMiddleware`**, **`Flow.ReactivePayload`**), **`MiddlewarePhase`** on **`MiddlewareContext`**, and **expected trace** (1+1 → before-add reactive → 2+2 → 4).

- 2026-03-28: **`GraphEntryPoint`** base; **optional** **`[GraphNodeDefinition]`** when inheriting definition base; **non-generic** **`GraphNodeDefinitionBase`** instead of **`VoidInstance`**; **definition → runtime node** mapping section; **`Initialize`** pipeline; **`GraphFlowCentralService`**; **keyboard** on **payload** + **entry** **`Initialize`**; **generic** **`ReactiveHookMiddleware`** + **`RuntimeGraphReactiveHooks`**; removed **`KeyboardGraphTrigger`** / bespoke **`GraphReactiveDispatchMiddleware`** from the canonical story.
