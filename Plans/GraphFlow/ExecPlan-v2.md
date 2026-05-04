# ExecPlan v2 — Graph Tooling

## Context

The previous attempt (`ExecPlan.md`, `Assets/GraphFlow/`) over-engineered a generic flow-graph engine that depended on a source generator that never landed. The result: per-type editor classes, per-type runtime definitions, per-type asset baker cases, hardcoded wiring seeds, half-implemented branching, dead-code services. Working tests, painful architecture.

This plan defines a focused, source-generator-driven, game-agnostic graph tool. The v1 driving use case is visual scripting of card abilities (the "Effect Graph") via Card Framework integration, but the package itself is generic — multiple `[GraphPackage]`-annotated runners (Effects, Dialogue, AI behavior, …) coexist in one project. Built on Unity Graph Toolkit.

The reference for the **designer-facing surface** is the existing FlowCanvas-based EffectFlow system in the Overknights project. The reference for the **runtime semantics** is the Card Framework planning docs (`C:\Unity\Card Framework\Product\`).

---

## Goals

1. Designers author card effects visually with zero code per effect.
2. Adding a new command or entry-point type to the game = write a payload class. Generator emits all editor/runtime/asset/registry plumbing.
3. Multiple entry points per card graph (`Play`, `Execute`, `Dispose`, `On Before Strike`, `On After Heal`, etc.).
4. `Validate` + `Run` dual flows on every entry node, native to the graph language.
5. Trigger listeners can pass-through-with-modifications, cancel, or replace the in-flight command.
6. Package is game-agnostic — works for Card Framework, FlowCanvas-shaped systems, or any custom convention via configuration.
7. Zero runtime reflection on hot paths. Per-execution port reads go through typed `InputPort<T>` / `OutputPort<T>` handles wired at hydration via the single `Connection.Bind` cast/conversion seam — direct delegate invocation, no reflection, no string lookups. The cast lives once at the type-erased baker→typed-port boundary, and is the architectural place where future converters plug in.

## Non-goals (v1)

1. **No macros / sub-graphs / cross-asset reuse.** Documented as v2 follow-up.
2. **No open generic payload types** (`DealDamage<TTarget>`). Closed types only.
3. **No graph-to-C# compilation.** Runtime is a tree-walking executor, not codegen of `Effect.Execute` bodies.
4. **No graph-tooling-defined zone or lifecycle.** Host owns when/how listeners register.
5. **No bespoke graph editor surface.** Graph Toolkit's default editor is the UX.
6. **No state-machine / behavior-tree semantics.** Graphs are async function compositions.

---

## Architecture overview

```
┌─────────────────────────────────────────────────────────────────────┐
│  CONSUMER (e.g. Overknights game)                                   │
│  • Subclasses GraphRunner; declares services on it                  │
│  • Defines payloads (IGraphEntry<R>/IGraphAction<R> markers,        │
│    or wraps existing domain types via [GraphPackage(CommandBase=)]) │
│  • Optionally implements IExecutable<R> on payloads (one class/node)│
│  • Optionally writes a DispatcherBase helper (Mode 2)               │
│  • Declares [assembly: GraphPackage(Runner = typeof(R), ...)]       │
│  • Game code drives controller.Run<T>() / Validate<T>()             │
└──────────────────┬──────────────────────────────────────────────────┘
                   │
        ┌──────────┴──────────┐
        ▼                     ▼
┌───────────────┐    ┌──────────────────────────────────────────────┐
│ ATTRIBUTES    │    │  PACKAGE                                      │
│ (zero-dep DLL)│    │  ├─ Editor assembly                           │
│               │    │  │  • GraphAssetImporterBase<TG, TR, TA>      │
│ [GraphPackage]│    │  │  • GraphBaker (bake-time translation)      │
│ [GraphPort]   │    │  │  • Hand-written generic nodes              │
│ [GraphHidden] │    │  │    (Cancel, Return*, Branch — flow;        │
│ [GraphMenu]   │    │  │     Not, predicates, math — pure data)     │
│ [GraphNode]   │    │  ├─ Runtime assembly                          │
│ [In] / [Out]  │    │  │  • GraphRunner (abstract)                  │
│               │    │  │  • RuntimeNode (data-node base)            │
│               │    │  │  • RuntimeNode<TRunner> (flow-node base)   │
│               │    │  │  • InputPort<T> / OutputPort<T> / FlowOut  │
│               │    │  │  • Connection / Connection<T> (the seam)   │
│               │    │  │  • IGraphEntry<R> / IGraphAction<R>        │
│               │    │  │  • IExecutable<R>                          │
│               │    │  │  • IInitializableNode<R> / IListenerNode<R>│
│               │    │  │  • GraphAsset<TRunner> (abstract SO base)  │
│               │    │  │  • Graph<TRunner> (Graph Toolkit base)     │
│               │    │  │  • GraphController<TRunner> (sealed)       │
│               │    │  │  • GraphExecutor<TRunner> (tree walker)    │
│               │    │  └─ Generator assembly (Roslyn)               │
│               │    │     • [assembly: GraphPackage] reader         │
│               │    │     • Convention strategies (4 built-in)      │
│               │    │     • Per-package + per-payload emission      │
└───────────────┘    └──────────────────────────────────────────────┘
                                │
                                ▼
                  ┌──────────────────────────────────┐
                  │  GENERATED ASSEMBLY              │
                  │  (one per consumer assembly)     │
                  │                                  │
                  │  Per [GraphPackage] declaration: │
                  │   • <R>Graph : Graph<R> (partial)│
                  │   • <R>GraphAsset : GraphAsset<R>│
                  │   • <R>GraphImporter             │
                  │                                  │
                  │  Per payload / per [GraphNode]:  │
                  │   • Editor Node subclasses       │
                  │   • Runtime node ctor + dict     │
                  │     population (Ports[id]=...)   │
                  │   • InitializePorts() partial    │
                  │     for OutputPort assignment    │
                  │   • Registry partial entries     │
                  └──────────────────────────────────┘
```

Three layers, three assemblies in the package, one generated assembly per consumer.

---

## Concept layer

### Two payload roles

`**IGraphEntry<TRunner>**` — object-specific, direct invocation. Only the graph that owns the entry node receives it. Marker interface shipped by the package.

```csharp
// PACKAGE — game-agnostic
public interface IGraphEntry<TRunner>  where TRunner : GraphRunner { }
public interface IGraphAction<TRunner> where TRunner : GraphRunner { }

// CONSUMER — fresh package
public sealed class Play    : IGraphEntry<MyRunner> { public bool special; }
public sealed class Execute : IGraphEntry<MyRunner> { }
public sealed class Attach  : IGraphEntry<MyRunner> { public Card target; }
```

`**IGraphAction<TRunner>**` — executed by their generated runtime node, either self-executing via `IExecutable<TRunner>` (Mode 1) or through a consumer-supplied `DispatcherBase` helper (Mode 2). When the consumer's `DispatcherBase` plugs into a domain pipeline (Card Framework's `IEffectScope.Dispatch`, etc.), other graphs can listen via `On Before X` / `On After X`. For Mode 1 packages with no pipeline, listener nodes simply aren't authored.

The runner type parameter is the **package discriminator**: every payload declares which graph package it belongs to. The same project can have multiple `[GraphPackage]`-annotated runners (Effects, Dialogue, …) and the generator partitions payloads by their `IGraphEntry<R>` / `IGraphAction<R>` argument. No silent grouping, no missing-attribute footguns, no shared bucket.

### Two binding modes — opt into the coupling, or stay clean

The marker-interface form above is **Mode 1** — best for fresh code being written against the graph tooling. The runner-typed marker is type-safe and makes the package association explicit at the declaration site.

**Mode 2** is for wrapping an existing domain hierarchy that you don't want to (or can't) modify. The classic case is the Card Framework, whose `Command<TResult>` hierarchy predates this package and shouldn't be tagged with anything graph-specific. Instead of touching the payload type, the consumer declares the binding on `[GraphPackage]`:

```csharp
[assembly: GraphPackage(
    Runner      = typeof(CardEffectRunner),
    CommandBase = typeof(Command<>),                  // existing Card Framework base
    Convention  = PortConvention.CommandResultPair,
    ...
)]

// Card Framework's command — completely untouched, no graph attribute, no marker interface
public sealed record Strike : Command<StrikeResult> {
    public int Magnitude;
    public Player Owner;
    public bool Unblockable;
}
```

The generator unions both sources for a given runner R: payloads implementing `IGraphAction<R>` plus payloads descending from any base type listed under that runner's `[GraphPackage]`. A consumer picks per-package which mode they want; both can coexist.

### Flows are typed functions

**Flow vs data (distinct persistence layers).** Graph Toolkit wires flow arrows and data wires through the same editor connection mechanism, but at bake time we split them:

- **`ConnectionRecord`** — **data only**. Hydration builds typed `Connection<T>` handles via `Connection.Bind` and stores them on the destination input port via `RuntimeNode.Bind`; flow is never wired through ports — it's walked by the executor against `flowEdges`.
- **`FlowEdge`** — **ordering only** (no value). The executor walks flow using `FlowContinuation` returned from `Execute` (which flow output port to follow) plus matching edges in `flowEdges`.

Flow ports are still "typed" in the authoring sense (Validate vs Run, Before vs After, Branch true/false); they are **not** modeled as fake `Connection<int>` carriers at runtime.

Every flow output port on an entry/listener node is a function with a known signature:


| Flow                                 | Inputs                  | Return                                       |
| ------------------------------------ | ----------------------- | -------------------------------------------- |
| Entry node — Validate                | Entry fields            | `bool`                                       |
| Entry node — Run                     | Entry fields            | `void`                                       |
| Trigger listener (Before) — Validate | Command fields          | `bool`                                       |
| Trigger listener (Before) — Run      | Command fields          | the Command (modified) — or Cancel / Replace |
| Trigger listener (After) — Validate  | Command + Result fields | `bool`                                       |
| Trigger listener (After) — Run       | Command + Result fields | `void`                                       |


Only **Before-trigger Run flows** have a non-bool, non-void return — and that return type is always the listened-to Command. This is the only place the generator emits a typed `Return T` node.

### Modification semantics for Before triggers

Three legal outcomes from a Before-trigger Run flow:

- **Pass-through (with optional field modifications)** — terminate via `[Return Strike]` (typed, generated). Each input port defaults to "use original value" if unwired (Graph Toolkit embedded port values).
- **Cancel** — terminate via generic `[Cancel]` node (takes a reason string). Aborts the in-flight command.
- **Replace** — terminate via generic `[Replace]` node (takes any command reference). Substitutes a different command into the pipeline.

Flow-end without a terminator on a non-void Run flow is a **graph-validation error** at edit time.

### Execution: `IExecutable<TRunner>` vs. `DispatcherBase`

For each payload, the generator decides at compile time how the emitted runtime node will execute it. Two strategies, picked per payload:

```csharp
// PACKAGE — opt-in interface for self-executing payloads
public interface IExecutable<TRunner> where TRunner : GraphRunner {
    ValueTask Execute(TRunner runner);
}
```

Decision tree (per payload):

1. Payload implements `IExecutable<TRunner>` → emit a runtime node whose `Execute(r)` calls `payload.Execute(r)`. **One class per node.**
2. Else, `[GraphPackage].DispatcherBase` declared → emit a subclass of that base, closed over the payload's types. The base provides the `Execute` body; the generator fills in `BuildPayload()` / `WriteOutputs()`.
3. Else → `EFG007` diagnostic: "Payload `Strike` has no execution path. Either implement `IExecutable<CardEffectRunner>`, or declare a `DispatcherBase` on `[GraphPackage]`."

`IExecutable` always wins if both are available. That gives a per-payload escape hatch — one weird command in an otherwise-uniform Card Framework package can self-execute without breaking the convention.

### Variables (deferred to v2)

v1 ships **no** designer-managed blackboard. Two paths cover the use cases:

- **Host-injected references** (Owner, Card, Parent, …) — typed properties on the consumer's runner subclass, read by consumer-authored accessor nodes.
- **Designer constants** (Magnitude = 7, EnergyCost = 3) — inline embedded port values via Graph Toolkit's `port.TryGetValue<T>` mechanism.

A full FIXED/MANAGED/VARIABLES blackboard with declared shared variables is a v2 follow-up. See "Host-injected references" below for the v1 substitute.

---

## Authoring surface

### Node taxonomy

**Generated per entry-shaped payload** (`IGraphEntry<TRunner>` implementer or `EntryBase` descendant — 1 editor node):

- `OnPlayNode`, `OnExecuteNode`, `OnDisposeNode`, … — direct-invoke entry nodes with `Validate` + `Run` flow output ports plus data output ports for the entry's fields.

**Generated per command-shaped payload** (`IGraphAction<TRunner>` implementer or `CommandBase` descendant — 3 editor nodes):

- `StrikeDispatcherNode` — used in graph bodies; ports = command's input fields + result's output fields (when paired). Execution path picked per the `IExecutable<TRunner>` vs `DispatcherBase` decision tree.
- `OnStrikeListenerNode` — entry-style with Before/After enum dropdown. Validate + Run flow ports. Output data ports for command fields (Before) or command + result fields (After). Registers with the consumer's pipeline through the controller's listener API.
- `ReturnStrikeNode` — typed terminator for Before-trigger Run flows. Input ports for command fields with "use original" defaults.

**Hand-written, shipped in the package** (finite set). All are `[GraphNode]`-annotated `partial` classes — runtime is hand-written, the generator emits the runtime ctor + editor mirror + registry entry. See "Generic-node emission" in the Source generator section for the full surface.

**Flow-bearing** (extend `RuntimeNode<TRunner>`, have `Execute` and at least one `FlowOut`):

- `Cancel` — generic early-exit. One reason-string input.
- `Replace` — generic early-exit. One typed command reference input.
- `Return` — void terminator. No inputs.
- `ReturnBool` — bool terminator. One bool input.
- `Branch` — flow control. Bool input, two `FlowOut` fields (`True` / `False`).

**Pure data** (extend `RuntimeNode` directly, no `Execute`, no `TRunner` — usable in any package):

- Predicates: `Equals<T>`, `NotEquals<T>`, `GreaterThan`, `LessThan`, `And`, `Or`, `Not`, `IsOfType`.
- Math: `Add`, `Subtract`, `Multiply`, `Divide`, `Modulo`. Numeric typing per Graph Toolkit conventions.
- Conversions: `ToString`, `ToInt`, etc. — minimal set as needed.

Multi-type-parameter pure data nodes (`Equals<T>` etc.) are deferred to M4 — each closed `T` needs its own editor node since the GraphToolkit picker can't pick at design time.

**Host-context accessors** — small set of consumer-defined `RuntimeNode<TRunner>` subclasses that read typed properties off the consumer's runner (e.g., `GetOwner` returns `runner.Owner`). The package defines no contract for these; the consumer writes whatever accessor nodes their game needs. Authored with the same `[GraphNode]` surface.

**Note on variables / blackboard.** v1 ships **without** a designer-managed blackboard. Designer constants live as inline port values (Graph Toolkit's embedded values). Host references are accessed via the host-context accessor nodes. A full FlowCanvas-style FIXED/MANAGED/VARIABLES blackboard is a v2 follow-up — the GT-native-vs-roll-our-own decision is deferred until we can spike against the real toolkit blackboard API.

### Inline payload editing

Graph Toolkit's **embedded port values** (`port.TryGetValue()`) carry the constant-or-default semantics. An unwired input port reads its constant from the node's serialized state. For `[Return Strike]` and command nodes, the convention is "unwired = original payload value" (for trigger listeners) or "unwired = required field, error" (for dispatcher nodes — must have a value).

### Validation in the editor

Implemented via Graph Toolkit's `OnGraphChanged(GraphLogger)`:

- Every entry/listener Run flow must terminate (Return / Cancel / Replace as appropriate).
- Validate flow on entry/listener must end at `[Return Bool]` if connected.
- Required input ports must be wired or have an embedded value.
- Generated and hand-written nodes carry their own validation hooks for per-type rules.
- Errors and warnings logged with node context for click-to-locate behavior in the Graph Toolkit editor.

---

## Source generator

### Architecture & reusability principles

The generator runs across multiple emission concerns (per-payload editor + runtime + registry entry, per-package trio, per-generic-node editor + registry entry — and more to come). Treat these as **passes layered on a single shared model**, not as independent walkers. Concretely:

- **One symbol walk per compilation.** `GraphPackageAssemblyParser` collects every type the generator cares about (payloads, generic nodes, package declarations) in a single pass and projects them into typed model records (`GraphPackageModel`, `PayloadModel`, `GenericNodeModel`, …). Emitters consume the model — they don't re-walk symbols.
- **Shared infrastructure, not shared code paths.** Port-ID derivation (FNV-1a + `[GraphPort(Id=…)]` overrides), field classification (`FieldClassifier`), naming (`RunnerStem`, `GraphCompilationNames`), and registry-entry composition live as standalone utilities reused by every emitter. New emitters compose; they don't duplicate.
- **Emitters are independent and ordered only by data dependency.** The trio emitter produces the registry shell; payload + generic-node emitters append entries to it via the same registry-entry builder. Adding a new emission concern is "new model record + new emitter + plug into registry composer," not "rewire the pipeline."
- **Snapshots cover every emitter.** Every emission path has at least one fixture in `Generators/Scaffold.GraphFlow.PackageGenerator.SnapshotTests/`. Adding an emitter without a snapshot is incomplete.

This is a hard constraint, not a style preference: per-payload work is already non-trivial, and M2 adds generic-node work that overlaps it. Without shared infra the generator becomes N parallel reflection-style walkers and we lose the compile-time guarantees that justified writing a generator at all.

### Configuration

A graph package is declared by one assembly-level attribute per runner. `AllowMultiple = true` — multi-package projects just stack them.

```csharp
[assembly: GraphPackage(
    Runner            = typeof(CardEffectRunner),
    Extension         = "card",                          // file extension; registers with Graph Toolkit
    AssetMenu         = "Effects/Card Effect",           // Project-window create-asset menu path
    Convention        = PortConvention.CommandResultPair,
    RegistryNamespace = "MyGame.Effects.Generated",

    // Mode 2 — bind existing domain hierarchies (no IGraphAction<R> on payloads).
    // Omit if all payloads in this package use IGraphEntry<R> / IGraphAction<R> markers (Mode 1).
    EntryBase   = typeof(EntryPoint),                    // optional, singular
    CommandBase = typeof(Command<>),                     // optional, singular open-generic

    // Optional helper bases the generator emits subclasses of (Mode 2 / mixed).
    // If a payload implements IExecutable<TRunner>, that path wins regardless.
    DispatcherBase = typeof(CardCommandDispatcher<,>),
    EntryNodeBase  = typeof(CardEntryPointNode<>),
    ListenerBase   = typeof(CardCommandListener<,>),
    ReturnBase     = typeof(ReturnPayloadNode<>)
)]
```

- **`Runner`** is the only required field outside Mode 1. It identifies which `[GraphPackage]` this is, partitions payloads by runner type, and parameterizes the generator-emitted Graph/Asset/Importer trio.
- **`typeof(...)`** is real C# — open generics in `typeof` are valid syntax. The generator reads symbols straight from the compilation; no string parsing, no namespace drift, refactor-safe.
- **`EntryBase` / `CommandBase`** are singular. If a consumer ever needs two distinct base hierarchies they split into two `[GraphPackage]` declarations — that's the right pressure, since two distinct hierarchies usually mean two distinct concerns.
- **`*Base` helper fields are optional.** They tell the generator what hand-written abstract class each emitted runtime node should inherit from. The open-generic forms (`` typeof(CardCommandDispatcher<,>) ``) get closed by the generator over the payload (and result) type — concrete emission looks like `class StrikeDispatcherRuntime : CardCommandDispatcher<Strike, StrikeResult>`. We keep all four specced; whether `EntryNodeBase`, `ListenerBase`, `ReturnBase` actually pull weight in v1 will be decided during Card Framework integration. Easy to drop later, harder to retrofit.

The runner stays clean — it's a services-only data carrier with no editor/generator metadata bolted on. All of that lives on the assembly-level config artifact.

### Future config UX (v2 follow-up)

The attribute form is intentionally Phase 1. Two follow-ups planned without changing the generator contract:

- **Phase 2 — asset-backed config.** A `GraphPackageConfigAsset` ScriptableObject with a custom inspector (type pickers for runner/bases, dropdown for convention, text fields for extension/menu). On save, a custom Editor regenerates a hidden `<name>.g.cs` partial containing the equivalent `[assembly: GraphPackage(...)]`. The Roslyn generator stays oblivious — it just sees the same attribute shape it would have read from a hand-written file. Asset is the human-facing source of truth; the auto-emitted `.g.cs` is the machine-facing source of truth for Roslyn. Foot-gun mitigation: header comment on the `.g.cs` (`// Auto-generated from Foo.asset. Do not edit.`) plus an `AssetPostprocessor` that overwrites unconditionally on asset change.
- **Phase 3 — wizard.** Project-window menu `Create > Graph Package` walks the user through runner type (or scaffolds a new one), extension, convention, base bindings, asset menu name. Outputs the asset + the runner stub + (via Phase 2's regen) the `.g.cs`. Designers and gameplay programmers never touch the attribute syntax.

Both phases are additive — no v1 client breaks when they land.

### Built-in conventions


| Convention             | Inputs                   | Outputs                                                     |
| ---------------------- | ------------------------ | ----------------------------------------------------------- |
| `CommandResultPair`    | command's public members | paired result's public members (via `CommandBase` `<TResult>`) |
| `AttributedFields`     | `[In]`-tagged            | `[Out]`-tagged                                              |
| `MutableInReadOnlyOut` | settable members         | init/get-only members                                       |
| `AllFieldsIn`          | all public members       | (none)                                                      |


`AttributedFields` and the per-type escape hatches (`[GraphHidden]`, `[GraphPort]`, `[GraphMenu]`) live in a tiny attributes-only DLL the package ships. Consumers reference it only if they use it.

### Per-payload emission

For each command-shaped payload (an `IGraphAction<TRunner>` implementer or a descendant of the package's `CommandBase`) with N input fields and M output fields, the generator emits:

- 3 editor `Node` subclasses (Dispatcher, Listener, Return)
- 3 runtime node classes (matching counterparts), each with:
  - Inline-value fields for designer constants (serialized into the asset by Unity)
  - Typed port handles: `InputPort<T>` field for each input, `OutputPort<T>` field for each output (constructed in the generated ctor with a reader that reads from the payload's matching member)
  - Generated default ctor that constructs each port handle and populates the inherited `Ports` dictionary (`Ports[id] = field`). Port id derivation is unchanged (FNV-1a + `[GraphPort(Id = N)]` override); the dict is the runtime lookup the hydration code consults.
- **`Execute` returns `ValueTask<FlowContinuation>`** — either stop the flow walk or name the **flow output port id** to follow next (Branch returns true/false port ids; linear nodes return their single flow-out id).
- One registry partial-class entry mapping the payload type id to the runtime node class plus an editor-port-name → port-ID lookup the baker uses when translating editor wires.

For each entry-shaped payload (an `IGraphEntry<TRunner>` implementer or a descendant of the package's `EntryBase`): same shape, just 1 editor + 1 runtime node instead of 3.

The pre-M2 emission (with hand-written `BindInput` / `GetOutputConnection` switches and an exposed `static class Ports`) is migrated to this dict-based form during M2; payload runtime emit and generic-node runtime emit share the same `Ports` dict + `Connection.Bind` plumbing through the inherited `RuntimeNode` base, so there is one wiring contract across the system instead of two.

### Generic-node emission (hand-written runtime, generator-completed partial)

Built-in generic nodes (`Branch`, `Cancel`, `Return`, `ReturnBool`, `Not`, …) and any consumer-authored equivalents are **runtime-hand-written, editor-generated, runtime-completed**. The author writes only the typed surface — port-handle fields and (for flow nodes) the `Execute` body. The generator emits a partial class completing the runtime + the editor mirror + the registry entry.

Design-iteration log (this section pivoted twice during M2 scoping; the current model is the third pass):

1. **First pass — class-level + field-level attributes.** `[GraphNode]` + `[FlowIn(id)]` / `[FlowOut(id, name)]` (class) + `[Input(id)]` / `[Output(id)]` (field). Author wrote a `static class Ports { public const int … = 0xF0F0_0001u; }` block, declared a `Connection<bool>?` field, and wrote a manual `BindInput` switch with `(Connection<T>)` casts. Rejected: triple bookkeeping (attribute id ↔ Ports const ↔ Execute usage), the cast was visible at every call site, and the `unchecked((int)0x...u)` literals were noise the author had to author.
2. **Second pass — typed handles + `OnInit` partial + cast in `InputPort<T>.AttachUntyped`.** Better, but the cast-helper was named badly, the `OnInit` example for output readers was hard to read, and a `_PortIds` static class still leaked into the .g.cs.
3. **Final pass (this section) — typed handles, dict-based dispatch, single `Connection.Bind` seam, `partial void InitializePorts()` for OutputPort assignment, no IDs on Input/Output ports, pure data nodes inherit from `RuntimeNode` (not `RuntimeNode<TRunner>`).**

#### Runtime support types

```csharp
namespace Scaffold.GraphFlow
{
    public abstract class Port { }                                // base, no logic

    public sealed class InputPort<T> : Port
    {
        Connection<T>? _conn;
        public T Read() => _conn != null ? _conn.Read() : default!;
        internal void Attach(Connection<T> c) => _conn = c;
    }

    public sealed class OutputPort<T> : Port
    {
        readonly Func<T> _read;
        public OutputPort(Func<T> read) { _read = read; }
        public T Read() => _read();
    }

    public readonly struct FlowOut                                // the only port that carries an id
    {
        readonly int _id;
        public FlowOut(int id) { _id = id; }
        public FlowContinuation Continue() => FlowContinuation.Next(_id);
    }
}
```

Three rules the runtime types enforce:

- **Single responsibility per port.** `InputPort<T>` provides values (`.Read()`). `OutputPort<T>` provides values (`.Read()`). Neither knows about the other side. Wiring is not a port responsibility.
- **No port IDs on `InputPort` / `OutputPort`.** The dict on the runtime node owns the `id → port` lookup; ports themselves don't carry the id. `FlowOut` is the one exception — it carries its id because `Execute` returns a `FlowContinuation` referencing it, and going through a reverse-lookup dict for that one access would be machinery for no gain.
- **`FlowIn` is editor-only metadata.** The executor walks `flowEdges` and calls `Execute` on the destination node directly; there is no runtime read at a flow-input. So no `FlowIn` field on the runtime class — the generator implicitly assigns one flow-input port (id 0) per flow-bearing node. Multi-flow-in cases (rare) are deferred to M4+.

#### `Connection` is the only cast/conversion seam

```csharp
public abstract class Connection
{
    public static Connection Bind(Port input, Port output) { /* the one cast */ }
}

public sealed class Connection<T> : Connection
{
    public InputPort<T>  Input  { get; }
    public OutputPort<T> Output { get; }
    public T Read() => Output.Read();
}
```

`Connection.Bind` is the **single** place in the system where untyped ports become typed wires. Future type-conversion (e.g., `int → string` auto-coercion via a registered converter) plugs in here, nowhere else. For M2 this is strict type equality — mismatch throws.

The cast lives at the type-erased boundary because the baker doesn't know `T` — it only has `(srcNodeId, srcPortId, dstNodeId, dstPortId)` ints. Pushing the cast anywhere else (into the port handles, into the generator-emitted partial) just relocates it; concentrating it in `Connection.Bind` makes the conversion seam discoverable and editable.

#### `RuntimeNode` base owns the dispatch

```csharp
public abstract class RuntimeNode
{
    public Dictionary<int, Port> Ports       { get; } = new();   // populated by generated ctor
    public List<Connection>      Connections { get; } = new();   // appended at hydration

    // Hydration calls this. No per-node override; the dict + Connection.Bind do the work.
    public Connection Bind(int dstPortId, RuntimeNode src, int srcPortId)
    {
        var c = Connection.Bind(Ports[dstPortId], src.Ports[srcPortId]);
        Connections.Add(c);
        return c;
    }
}

public abstract class RuntimeNode<TRunner> : RuntimeNode where TRunner : GraphRunner
{
    public abstract Task<FlowContinuation> Execute(TRunner runner);
}
```

Two consequences:

- **Pure data nodes inherit from `RuntimeNode` directly.** `Not`, `Equals<T>`, `IntToString` — no `TRunner`, no `Execute`. They're data transforms, not flow nodes.
- **Flow nodes inherit from `RuntimeNode<TRunner>`.** `Branch`, `Cancel`, `Return`, `ReturnBool`, entries, dispatchers, listeners. They have `Execute` and may participate in `flowEdges`.

The package's per-runner registry's factory delegate type relaxes from `Func<RuntimeNode<TRunner>>` to `Func<RuntimeNode>` so pure data nodes can register. The executor still only invokes `Execute` on `RuntimeNode<TRunner>` instances — pure data nodes never appear in `flowEdges` and never receive `Execute` calls.

#### Author surface — `Branch` (flow-bearing)

```csharp
[GraphNode(Category = "Flow")]
public partial sealed class Branch<TRunner> : RuntimeNode<TRunner>
    where TRunner : GraphRunner
{
    public InputPort<bool> Condition;
    public FlowOut True;
    public FlowOut False;

    public override Task<FlowContinuation> Execute(TRunner runner) =>
        Task.FromResult(Condition.Read() ? True.Continue() : False.Continue());
}
```

#### Generator output — `Branch.g.cs`

```csharp
partial class Branch<TRunner>
{
    public Branch()
    {
        Condition = new InputPort<bool>();
        True  = new FlowOut(2);
        False = new FlowOut(3);
        Ports.Add(1, Condition);
    }
}
```

#### Author surface — `Not` (pure data)

```csharp
[GraphNode(Category = "Logic")]
public partial sealed class Not : RuntimeNode
{
    public InputPort<bool> Value;
    public OutputPort<bool> Result;

    partial void InitializePorts() =>
        Result = new OutputPort<bool>(() => !Value.Read());
}
```

No `Execute`. No `TRunner`. `Result`'s reader closes over `Value` so its computation is driven by whatever upstream node is wired into `Value` at hydration.

#### Generator output — `Not.g.cs`

```csharp
partial class Not
{
    public Not()
    {
        Value = new InputPort<bool>();
        InitializePorts();        // user fills OutputPort lambdas here
        Ports.Add(1, Value);
        Ports.Add(2, Result);
    }
    partial void InitializePorts();
}
```

#### Generator contract for `[GraphNode]` types

For each `[GraphNode]`-annotated class deriving (directly or transitively) from `RuntimeNode` (with an optional single `TRunner` type parameter), the generator emits **per package** that uses the runner:

- **Runtime partial** (`<Name>.g.cs`) — default ctor that constructs `InputPort<T>` and `FlowOut` fields, calls user's `InitializePorts()` partial method (declared in the same partial as a no-body `partial void`), then populates the `Ports` dict with all data ports (Input + Output, not FlowOut). Generator owns the ctor; user owns the typed surface and `Execute`/`InitializePorts` bodies.
- **Editor mirror** (`<Name>EditorNode.g.cs`) — same shape as today: port-name consts + `OnDefinePorts` declaring flow ports as arrowheads and data ports as typed Graph Toolkit ports. For pure data nodes, no flow ports.
- **Registry entry** — closes the open generic at the package's runner if needed (`new Branch<MySmokeRunner>()`); pure data nodes get an unparameterized factory (`new Not()`). Flow/data port-name → port-id maps as before.

Port IDs are assigned sequentially in source order: FlowIn = 0 (implicit, flow nodes only); fields are numbered 1, 2, 3 … in declaration order. `[GraphPort(Id = N)]` on a field still pins an id across renames. Port IDs per node only need to be stable across re-bakes; they don't need to be globally unique.

#### Convention summary

| Concept | User writes | Generator emits |
| --- | --- | --- |
| Class declaration | `[GraphNode] partial sealed class Foo : RuntimeNode[<TRunner>]` | (nothing — user owns class declaration) |
| Data input | `public InputPort<T> Foo;` (field) | construction in ctor, `Ports.Add(id, Foo)` |
| Data output | `public OutputPort<T> Foo;` (field) + reader assignment in `InitializePorts` | construction-call dispatch, `Ports.Add(id, Foo)` |
| Flow output | `public FlowOut Foo;` (field) | `new FlowOut(id)` in ctor |
| Flow input | (nothing — implicit at port id 0 for flow nodes) | port id 0 in editor mirror; `flowEdges` references it |
| Flow logic | `Execute(TRunner)` body | (nothing — user owns) |
| Output computation | reader lambda in `InitializePorts` body | `partial void InitializePorts();` declaration |
| Port-id table | (nothing) | implicit in dict-population code |

Zero casts in either file. Zero port-id literals in the user file. Single source of truth per port: its declared field.

#### Constraints & deferrals

- **Single TRunner type parameter.** The parser accepts non-generic data nodes (`Not`) or flow nodes generic over exactly `TRunner` (`Branch<TRunner>`). Multi-type-parameter nodes (`Equals<T>`, `IsOfType<T>`) need per-T closed instantiations in the editor mirror — **deferred to M4** (each closed T needs its own editor node so the GToolkit picker can offer it; we'll spec the registration shape when we get there).
- **No multi-flow-in.** One implicit FlowIn per flow node. If a node legitimately needs multiple (e.g., a `Join` that fans flow back together), spec via an explicit `FlowIn` field in M4+.
- **Connection conversion not implemented.** `Connection.Bind` checks `T == T` and constructs `Connection<T>`; mismatch throws. The architectural seam is in place for converters; the converter mechanism itself is M4+.

Consumer-authored generic nodes use the exact same surface and pipeline — there is no built-in vs. user-written split in the generator. The package ships a finite first set; everything else extends through the same surface.

The runtime node's base class is picked per-payload by the **execution decision tree** (see "Execution" in the Concept layer):

```
if payload implements IExecutable<TRunner>:
    emit:  class StrikeDispatcherRuntime : RuntimeNode<TRunner> {
               override Execute(r) => payload.Execute(r);   // payload is typed
           }
elif [GraphPackage].DispatcherBase declared:
    emit:  class StrikeDispatcherRuntime : CardCommandDispatcher<Strike, StrikeResult> {
               override BuildPayload() => /* read input slots */;
               override WriteOutputs(r) => /* write output slots */;
           }
else:
    diagnostic EFG007 (no execution path)
```

Three invariants the generator must hold:

1. **Port IDs are stable across source-order changes.** Adding a new field reserves a fresh ID; removing one retires it; an existing field keeps its ID even if the field above it is deleted. Default derivation is FNV-1a of the field name. `[GraphPort(Id = N)]` lets the consumer freeze an ID across a rename.
2. **Port IDs in the runtime ctor + editor port-name lookup are the single source of truth.** Both the runtime node's `Ports[id] = field` registration in the generated ctor and the registry's editor-port-name → port-ID lookup the baker reads come from the same id derivation. There's no second list to keep in sync.
3. **Payload-to-runner partition is enforced.** A payload satisfying bindings for two different `[GraphPackage]` declarations is `EFG008`. Each payload belongs to exactly one package.

### Per-package emission (the Graph/Asset/Importer trio)

For each `[GraphPackage]` declaration, the generator additionally emits the boilerplate trio that the consumer used to write by hand:

- `<RunnerName>Graph : Graph<TRunner>` — `partial class` so the consumer can extend it (custom `OnGraphChanged`, additional menu items, etc.). Carries `[Graph(extension)]` and the create-asset menu item.
- `<RunnerName>GraphAsset : GraphAsset<TRunner>` — concrete `ScriptableObject` type so Unity can serialize it.
- `<RunnerName>GraphImporter : GraphAssetImporterBase<<RunnerName>Graph, TRunner, <RunnerName>GraphAsset>` — `[ScriptedImporter]`-annotated, registers the bake pipeline against the file extension.

The naming convention is mechanical: strip a trailing `Runner` suffix, prepend to `Graph` / `GraphAsset` / `GraphImporter`. `CardEffectRunner` → `CardEffectGraph`, `CardEffectGraphAsset`, `CardEffectGraphImporter`.

**Escape hatch.** If the consumer hand-writes any of those three (e.g. they need a custom importer that bumps `version: N` or runs additional post-bake steps), the generator detects the existing declaration and skips emission for that one. The other two are still auto-emitted. No all-or-nothing trade-off.

### Resolved generator behavior


| Decision                    | Resolution                                          |
| --------------------------- | --------------------------------------------------- |
| Config required             | At least one `[assembly: GraphPackage]`; otherwise generator emits nothing |
| Multi-package               | `AllowMultiple = true` on the attribute; one declaration per runner |
| Conventions per package     | One per `[GraphPackage]`; different packages can use different conventions |
| Abstract bases              | Skipped automatically, only instantiable types emit |
| Open generics in payloads   | Not supported in v1; emits warning if encountered   |
| Open generics in `*Base`    | Required form (`typeof(CardCommandDispatcher<,>)`); generator closes per payload |
| Registry shape              | `static partial class` so consumers can extend      |
| Graph/Asset/Importer trio   | Auto-emitted per `[GraphPackage]`; consumer-written declarations override (skip emission) |
| Nullable types              | Pass through as-is                                  |
| Field ordering              | Source order for editor display; **does NOT determine port ID** (port IDs are stable hashes) |
| Port ID stability           | Stable int per field (default: FNV-1a of name); `[GraphPort(Id = N)]` to freeze across renames |
| Payload-to-package partition | Each payload belongs to exactly one package; multi-binding → `EFG008` |


### Compile-time diagnostics (nice-to-have)

(`EFG001` reserved — was "config required" in earlier drafts; now a missing `[GraphPackage]` is a silent no-op since the generator simply emits nothing.)

- `EFG002` — `[In]` on a `readonly` field
- `EFG003` — `[Out]` on a settable field (in `AttributedFields` mode)
- `EFG004` — Field type not serializable
- `EFG005` — Command-shaped payload without paired result type (in `CommandResultPair` mode)
- `EFG006` — Field name conflicts with a generated port label
- `EFG007` — Payload has no execution path (no `IExecutable<TRunner>`, no `DispatcherBase` declared)
- `EFG008` — Payload satisfies bindings for two different `[GraphPackage]` declarations
- `EFG010` — `[GraphNode]` class has an `OutputPort<T>` field but no `InitializePorts` partial method body assigning it (the field would remain null at construction)
- `EFG011` — `[GraphNode]` class has more than one type parameter, or its single type parameter is not constrained to `GraphRunner` (deferred multi-T case — emit warning, skip emission)

Ship the structural ones (EFG005, EFG007, EFG008, EFG010, EFG011); add the rest as friction emerges.

---

## Runtime

### Two distinct graph models

It's important up front that **Graph Toolkit and our runtime do not share a graph format**. Graph Toolkit owns the *editor* model (designer authoring view); we own the *runtime* model. The bake step is a real translation that can validate, can fail, and can produce 1:N or N:1 mappings between editor and runtime nodes.


| Layer             | Owned by      | Persisted by                                                                       | Contains                                                                                          |
| ----------------- | ------------- | ---------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| **Editor graph**  | Graph Toolkit | Graph Toolkit (auto-YAML in the consumer-extension source file, e.g. `.cardgraph`) | Designer-authored nodes, port connections, embedded port values, node positions                   |
| **Runtime asset** | Our package   | Unity (`ScriptableObject` sub-asset of the same file)                              | Baked runtime nodes, resolved connections, entry-point index, schema version                      |
| **Runtime tree**  | Our package   | In-memory only at game time                                                        | Pointer-resolved `RuntimeNode<TRunner>` instances, port-binding delegates, hydrated payload state |


The editor graph never runs. The runtime asset is what game code references and what the executor walks. The runtime tree is constructed from the runtime asset on `Initialize`.

### Asset lifecycle (single source file, dual representation)

Following Unity's `VisualNovelDirector` sample: **one source file on disk** (with an extension the consumer picks via `[GraphPackage(Extension = "...")]` — e.g. `.cardgraph` for the Card Framework integration) holds both the editor graph (hidden, for re-editing) and the imported runtime asset (the `SetMainObject`, what consumers reference). Designers drag the file into a generator-emitted concrete `GraphAsset<TRunner>` subclass field (e.g. `CardEffectGraphAsset`) and get the runtime form directly.

**Custom Graph subclass** — registers the file extension and is the Graph Toolkit edit-time asset type. **Auto-emitted by the source generator** from `[assembly: GraphPackage(Runner = typeof(...), Extension = "...")]`. The consumer doesn't write it. Generator output looks like:

```csharp
// AUTO-GENERATED — example for [GraphPackage(Runner = typeof(CardEffectRunner), Extension = "cardgraph")]
[Graph("cardgraph")]
public partial class CardEffectGraph : Graph<CardEffectRunner> {
    [MenuItem("Assets/Create/Effects/Card Effect")]
    static void CreateNew() =>
        GraphDatabase.PromptInProjectBrowserToCreateNewAsset<CardEffectGraph>("New Effect");
}
```

The class is `partial` so the consumer can extend it with custom edit-time behavior — e.g. an `OnGraphChanged` override for additional validation:

```csharp
// CONSUMER — optional partial extension, only when custom edit-time logic is needed
public partial class CardEffectGraph {
    public override void OnGraphChanged(GraphLogger logger) {
        // Light edit-time validation — flags errors as designers wire (terminator presence,
        // type mismatches, unwired required ports). Drives inline error UI;
        // does NOT determine whether the bake succeeds.
    }
}
```

If the consumer hand-writes the whole class (full override of name/namespace/etc.), the generator detects the existing `[Graph("cardgraph")]` declaration and skips emission for that one. The other two trio members (`<R>GraphAsset`, `<R>GraphImporter`) are still auto-emitted independently.

`[Graph("cardgraph")]` registers the extension with Unity. `PromptInProjectBrowserToCreateNewAsset` is the standard creation API — no `[CreateAssetMenu]`, no custom `CreateInstance` plumbing. The package supplies the `Graph<TRunner>` base; the generator pairs it with the consumer's runner.

**What Graph Toolkit serializes for us** (auto, into the `.cardgraph` YAML):

- Editor node identities, types, positions
- Port-to-port connections (between editor nodes only)
- Embedded port values (constants on unwired ports)
- Per-node `[Serializable]` fields

**What Graph Toolkit does NOT serialize:**

- Anything runtime-shaped: no resolved port-binding delegates, no payload instances, no execution order, no flow successor pointers, no validated tree structure.

v1 does not use Graph Toolkit's blackboard surface; designer constants live as embedded port values, host-context references live on the consumer's runner. The editor graph is just a designer's authoring artifact. It can be in any state — including states that don't bake. Our runtime model is separate.

### The bake step (editor → runtime)

The ScriptedImporter is where editor connections become runtime connections. It is a real translation, not a copy. The package provides a reusable abstract base class; the generator emits the concrete subclass per `[GraphPackage]`:

```csharp
// PACKAGE — reusable base
public abstract class GraphAssetImporterBase<TGraph, TRunner, TAsset> : ScriptedImporter
    where TGraph  : Graph<TRunner>
    where TRunner : GraphRunner
    where TAsset  : GraphAsset<TRunner>
{
    public override void OnImportAsset(AssetImportContext ctx) {
        var editorGraph = GraphDatabase.LoadGraphForImporter<TGraph>(ctx.assetPath);
        if (editorGraph == null) {
            ctx.LogImportError("Failed to load graph; asset is corrupt or version-mismatched.");
            return;
        }

        var bakeResult = GraphBaker.Bake<TGraph, TRunner, TAsset>(editorGraph);
        foreach (var diag in bakeResult.Diagnostics)
            ctx.LogImportError(diag.Message, ctx.assetPath);

        if (bakeResult.HasErrors) return;

        ctx.AddObjectToAsset("Runtime", bakeResult.Asset);
        ctx.SetMainObject(bakeResult.Asset);
    }
}

// AUTO-GENERATED — concrete subclass from [GraphPackage]
[ScriptedImporter(version: 1, ext: "cardgraph")]
internal sealed class CardEffectGraphImporter
    : GraphAssetImporterBase<CardEffectGraph, CardEffectRunner, CardEffectGraphAsset> { }
```

If the consumer needs to bump `version: N` or add post-bake steps, they hand-write the importer subclass and the generator skips emission for that one.

`GraphBaker` is responsible for **everything that turns an editor graph into a runtime-compliant artifact**:

1. **Node-id assignment (stable across re-bakes).** The baker reads the previous runtime asset at `ctx.assetPath` (if any) and walks its `nodes`, recovering an `editorGuid → nodeId` map from each runtime node's `editorGuid` field. Every editor node Graph Toolkit reports gets translated to a stable `int nodeId`: an existing editor guid keeps its previous id; a new editor node gets the next free monotonic int (highest seen + 1). Removed editor nodes' ids are retired. Each emitted runtime node carries both its new `nodeId` and the source `editorGuid` so the next re-import can recover the map. Runtime hydration ignores `editorGuid`.
2. **Node translation.** Each editor node maps to one or more typed runtime node instances. The baker reads the registry to find the **generator-emitted factory delegate** (`Func<RuntimeNode>` — relaxed from `Func<RuntimeNode<TRunner>>` so pure data nodes can register; flow-bearing nodes still derive from `RuntimeNode<TRunner>` and the executor's typed walker only invokes `Execute` on those), invokes it to construct the runtime node, populates its inline-value fields from the editor node's port constants (via `port.TryGetValue<T>()` for unwired embedded values, or `port.firstConnectedPort` chasing for variable/constant nodes — same approach as VND's `GetInputPortValue`), and assigns the stable `nodeId` from step 1. No `Activator.CreateInstance`, no reflection — the factory delegate is one of the pieces the source generator emits per payload / per `[GraphNode]` class. Most editor nodes map 1:1 (`StrikeDispatcherNode` → `StrikeDispatcherRuntime`); some may be 1:N if a built-in node compiles to multiple runtime steps.
3. **Connection resolution (data only).** Editor **data** port-to-port connections are translated into `ConnectionRecord`s of `(fromNodeId, fromPortId, toNodeId, toPortId)` — all four ints. **Flow** connections are translated into `FlowEdge`s of `(fromNodeId, fromFlowPortId, toNodeId, toFlowPortId)` — execution ordering only; no `Connection<T>`. The bake step writes only int tuples; **typed `Connection<T>` objects are not built at bake**. They get built at hydration time when the controller calls `dst.Bind(dstPortId, src, srcPortId)` — `RuntimeNode.Bind` looks up both ports through the dict and calls the static `Connection.Bind(input, output)` seam, where the cast happens once and the typed wire gets stored on the input port + appended to the destination's `Connections` list.
4. **Embedded value extraction.** Each unwired input port's constant value is read via `port.TryGetValue<T>()` and stored into the matching typed field on the runtime node instance.
5. **Bake-time validation.** Errors that prevent runtime correctness are reported here, even if the editor accepted them:
  - Required ports unwired and lacking a constant
  - Type mismatches Graph Toolkit didn't catch
  - **A connection's `fromPortId` or `toPortId` no longer exists on its node's runtime type** (catches generator-side breakage when a payload field is removed)
  - Missing terminators on non-void Run flows (`[Return X]` / `[Cancel]` / `[Replace]`)
  - Cycles in flow paths
  - Entry node referring to a payload type that no longer exists in the registry
  - Listener node referring to a command-shaped payload type that's been removed
6. **Entry-point indexing.** Walks all entry/listener nodes, builds the `entries` list mapping bound `entryTypeId` (string registry key) to the runtime root `nodeId` (int).
7. **Schema version stamping.** The runtime asset records the current schema version (paired with the importer version — see asset versioning below).

A graph that passes `OnGraphChanged` validation can still **fail to bake**. That's fine and expected — `OnGraphChanged` runs on every keystroke and does cheap structural checks; the baker does deeper analysis (whole-graph cycles, registry lookups, type cross-validation). On bake failure, no runtime asset is emitted; the source file becomes "edits but doesn't build" and the Inspector / Console explains why.

### Runtime asset format

The runtime asset is a real Unity `ScriptableObject` — added as a sub-asset and made the main object via `ctx.SetMainObject` (same pattern as the VisualNovelDirector sample). That means: it survives builds, ships through Addressables, can be referenced from prefabs and scenes by GUID, shows up in the Inspector. **It is an asset, not a serializable POCO.** The editor graph stays inside the same source file as a non-main object; nothing at runtime touches it.

Serialization is **plain Unity ScriptableObject serialization** — we ride Unity's machinery, we do not layer a custom one on top.

The package ships abstract, runner-typed base classes (`RuntimeNode`, `RuntimeNode<TRunner>`, `GraphAsset<TRunner>`) plus the wiring object (`Connection`/`Connection<T>`) and serializable record types (`ConnectionRecord`, `FlowEdge`, `EntryIndex`). **Entry nodes** use a typed base `EntryRuntimeNode<TEntry, TRunner>` so `GraphController.Run<TEntry>(payload)` can call `SetPayload` without ad-hoc interfaces. The package is game-agnostic; it never names "effect" or anything domain-specific. The concrete `GraphAsset<TRunner>` subclass per package is **generator-emitted** from `[GraphPackage]` (see "Per-package emission" in the source generator section); the consumer doesn't write it:

```csharp
// PACKAGE — game-agnostic

// Connection — the runtime wiring object. Built at hydration, never serialized.
public abstract class Connection {
    public abstract object SourceNodeBoxed   { get; }   // back-ref for traversal / diagnostics
    public abstract int    SourcePortId      { get; }
}

public sealed class Connection<T> : Connection {
    private readonly Func<T> _read;
    public RuntimeNode  SourceNode    { get; }
    public override object SourceNodeBoxed => SourceNode;
    public override int    SourcePortId    { get; }

    internal Connection(RuntimeNode source, int sourcePortId, Func<T> read) {
        SourceNode = source; SourcePortId = sourcePortId; _read = read;
    }

    public T Read() => _read();
}

[Serializable]
public abstract class RuntimeNode {
    public int    nodeId;              // stable integer id, assigned at bake, persisted across re-bakes
    public string editorGuid;          // GT's stable id for the source editor node — only used by the baker
                                       // on re-import to recover the editorGuid → nodeId mapping. Runtime ignores it.

    public Dictionary<int, Port> Ports       { get; } = new();   // populated by generated ctor
    public List<Connection>      Connections { get; } = new();   // appended at hydration

    // Hydration calls this. The cast lives once, inside Connection.Bind. No per-node override.
    public Connection Bind(int dstPortId, RuntimeNode src, int srcPortId) {
        var c = Connection.Bind(Ports[dstPortId], src.Ports[srcPortId]);
        Connections.Add(c);
        return c;
    }
}

[Serializable]
public abstract class RuntimeNode<TRunner> : RuntimeNode where TRunner : GraphRunner {
    public abstract ValueTask<FlowContinuation> Execute(TRunner runner);
}

// Data edges — typed values via Connection<T> at hydration.
[Serializable]
public struct ConnectionRecord {
    public int fromNodeId;
    public int fromPortId;
    public int toNodeId;
    public int toPortId;
}

// Flow edges — ordering only; executor walks using Execute's FlowContinuation.
[Serializable]
public struct FlowEdge {
    public int fromNodeId;
    public int fromFlowPortId;
    public int toNodeId;
    public int toFlowPortId;
}

[Serializable]
public struct EntryIndex {
    public string entryTypeId;
    public int    rootNodeId;
}

public abstract class GraphAsset<TRunner> : ScriptableObject where TRunner : GraphRunner {
    [SerializeReference] public List<RuntimeNode<TRunner>> nodes;
          // typed, polymorphic
    public                List<ConnectionRecord>           connections; // data
    public                List<FlowEdge>                   flowEdges;     // execution order
    public                List<EntryIndex>                 entries;
    public                int                              schemaVersion;
}
```

Concrete subclass — **auto-emitted by the source generator** from `[GraphPackage]`:

```csharp
// AUTO-GENERATED — example for [GraphPackage(Runner = typeof(CardEffectRunner), ...)]
public sealed class CardEffectGraphAsset : GraphAsset<CardEffectRunner> { }
```

The concrete subclass exists because Unity needs a concrete `ScriptableObject` type per asset (it can't serialize an open generic). The generator emits a one-line file per `[GraphPackage]`. Designers drag the imported asset into `CardEffectGraphAsset`-typed fields. A consumer can hand-write the class to override its name/namespace; the generator detects the existing declaration and skips emission for that one.

Why typed `[SerializeReference]` nodes instead of an opaque `byte[] serializedConstants` blob: the source generator already emits a runtime class per payload type, so each node has a known C# shape. Storing that shape directly means

- Unity serializes fields normally — `Sprite`, `AssetReferenceT<>`, `UnityEngine.Object` references all patch up correctly through the build pipeline (the VND sample relies on this for `BackgroundSprite`).
- The runtime asset is Inspector-debuggable.
- Hydration reads already-typed objects; no per-node `Deserialize(blob)` step, no `typeId` string lookup at load time (the C# type *is* the identity for already-instantiated nodes).
- No hand-written serializer/deserializer to maintain.

`typeId` strings still exist, but only at the **registry/bake** layer — they're how the baker maps a Graph Toolkit editor node to the runtime node class to instantiate. Once instantiated and stored in the asset, the runtime carries the live typed instance.

No `variables` collection in v1 — the blackboard concept is deferred. Designer constants are baked into the corresponding fields on the typed runtime nodes; host-injected references come off the consumer's runner. When v2 adds a blackboard, we'll add a `variables` field and bump `schemaVersion` + the importer version.

Two concrete properties of these records:

- **Every reference in a `ConnectionRecord` is an ID, never an index.** Both `nodeId` and `portId` are stable integers assigned at bake/generation. List positions in `asset.nodes` and physical port order in source code are explicitly NOT used as identity — any reorder during a re-bake or generator change must not break existing connections.
- **Connection records reference runtime node ids only.** Editor-only helper nodes that get inlined or dropped during bake do not appear; the connection collapses to bind directly to the upstream non-helper source.

#### Connection model — references survive via stable IDs, not pointers

This is the core thing that has to work and the thing VND does **not** exercise (VND is a linear sequence with no port-to-port connections). The reference model is FlowCanvas-shaped — stable IDs at both endpoints, no C# pointers in the persisted form, references re-resolved fresh on every load:

- **Stable `nodeId` (int).** Assigned at first bake and persisted across re-imports. The baker maintains an `editorGuid → nodeId` map (Graph Toolkit assigns each editor node a stable guid; the baker translates it to a monotonic int and remembers the mapping by reading the previous runtime asset on re-import). An unchanged editor node keeps its int id forever; a newly-created editor node gets the next free int. Destroying the editor node retires the id permanently.
- **Stable `portId` (int).** Assigned by the source generator, derived deterministically from the C# field name (e.g. FNV-1a hash) so that source-order changes don't move IDs. An explicit `[GraphPort(Id = N)]` escape hatch lets a consumer freeze an ID across a rename. Adding a new field reserves a fresh ID; removing one retires it. The generator uses these IDs as the keys when populating each runtime node's `Ports` dict in its emitted ctor, and the same IDs are what the baker writes into `ConnectionRecord` / `FlowEdge` after translating editor port names. There is no exposed `static class Ports` block; the dict is the single port-id binding the generator and baker share.
- **`ConnectionRecord` is four ints (data only).** `(fromNodeId, fromPortId, toNodeId, toPortId)`. No strings, no indices. Unity serializes them as plain blittable ints inside the SO.
- **`FlowEdge` is four ints (flow only).** `(fromNodeId, fromFlowPortId, toNodeId, toFlowPortId)`. Matched after each node's `Execute` returns `FlowContinuation.Next(outFlowPortId)`.
- **Hydration recovers the references.** A `Dictionary<int, RuntimeNode<TRunner>>` indexes the asset's nodes by id; each connection looks up both endpoints by id and produces a typed `Connection<T>` wiring object. See the "Wiring mechanism" subsection under Hydration for the no-reflection mechanics.

That's the durable artifact: the connection between A's port X and B's port Y is **four ints** in the SO. Unity's serializer handles it without any custom pipeline. References get re-resolved fresh on every load.

**Editor positions are not in the runtime asset** — they live in the editor graph (Graph Toolkit handles them). Round-trip editing works because reopening the file shows the editor graph, not the runtime asset.

### Hydration — turning records into a wired runtime tree

This is the step the implementer needs to execute exactly right, because it's where serialized records become a live, port-wired tree the executor can walk. `GraphController<TRunner>.Initialize(runner)` is the entry point.

Pre-condition when `Initialize` is called: the consumer has loaded a `GraphAsset<TRunner>` (Resources, Addressables, direct reference — doesn't matter to the controller). Unity has already deserialized the asset, so:

- `asset.nodes` is a `List<RuntimeNode<TRunner>>` of **live, typed instances** with their inline-value fields populated. No factory step needed.
- `asset.connections` is a `List<ConnectionRecord>` (data wiring only).
- `asset.flowEdges` is a `List<FlowEdge>` (execution order — no `Connection<T>`).
- `asset.entries` is a `List<EntryIndex>` mapping payload type id → root `nodeId` (int).

Hydration steps, in order:

1. **Index nodes by id.** Build `var byId = new Dictionary<int, RuntimeNode<TRunner>>(asset.nodes.Count);` then `foreach (var n in asset.nodes) byId.Add(n.nodeId, n);`. Int-keyed dictionary, fast hash, no string allocation.

2. **Wire data connections.** For each `ConnectionRecord c` (data ports only):
   ```csharp
   var from = byId[c.fromNodeId];
   var to   = byId[c.toNodeId];
   to.Bind(c.toPortId, from, c.fromPortId);   // base impl: dict lookup → Connection.Bind → store on input port + Connections list
   ```
   One method call per **data** edge. The cast happens once inside `Connection.Bind`; the input port stores the typed `Connection<T>` and reads through it. Flow is wired separately via `flowEdges` and never goes through ports.

3. **Bind entry payloads at dispatch.** When `Run<TEntry>(payload)` runs, the controller calls `SetPayload` on the root when it inherits `EntryRuntimeNode<TEntry, TRunner>`.

4. **Flow execution (separate from hydration).** The executor starts at the entry root, runs `Execute`, reads `FlowContinuation`, and follows `flowEdges` by matching `(fromNodeId, fromFlowPortId)` — then repeats on the target node. Pure data nodes reached only via flow may run when flow enters them (linear graphs); Branch (M2) selects among multiple outgoing flow ports via `FlowContinuation`.

5. **Build the entry index.** `var entryByType = new Dictionary<Type, RuntimeNode<TRunner>>();` Walk `asset.entries`, resolve `entryTypeId` to `Type` (registry / `AssemblyQualifiedName` in M0), look up the root node in `byId` by `rootNodeId`, populate the dictionary.

6. **Lifecycle pass.** Walk `asset.nodes` once and:
   - For any node implementing `IInitializableNode<TRunner>`, call `Initialize(runner)`. Nodes can cache typed services off the runner here.
   - For any node implementing `IListenerNode<TRunner>`, collect it into `CommandListeners` for the consumer to register with their pipeline.

7. **Done.** The controller holds hydrated data wiring (`Connection<T>` on inputs), entry lookup, and listener hooks. The executor walks flow separately using `flowEdges` + `FlowContinuation`.

#### Wiring mechanism — no reflection

The "lookup" in step 2 is **a dictionary read + a single cast**, both occupying contiguous one-time work at hydration.

For each generated runtime node (payload-driven or `[GraphNode]`-driven), the generator emits the same wiring scaffolding: a default ctor that constructs typed port handles (`InputPort<T>`, `OutputPort<T>`, `FlowOut`), populates the inherited `Ports` dictionary keyed by port id, and (for nodes with `OutputPort<T>` fields) calls a user-defined `partial void InitializePorts()` body that assigns the output readers.

Wiring scaffolding example (Mode 2 / DispatcherBase path — the same dict/port shape applies to entry, listener, and `[GraphNode]` runtimes):

```csharp
// Generated, per payload — example: StrikeDispatcherRuntime
public sealed partial class StrikeDispatcherRuntime : CardCommandDispatcher<Strike, StrikeResult> {
    // Inline designer constants — serialized into the asset by Unity
    public int  Magnitude;
    public Card Target;

    // Typed port handles — runtime-only, NOT serialized. Port IDs live only in the Ports dict.
    public InputPort<int>   InMagnitude;
    public InputPort<Card>  InTarget;
    public OutputPort<int>  OutDamageDealt;

    public StrikeDispatcherRuntime() {
        InMagnitude    = new InputPort<int>();
        InTarget       = new InputPort<Card>();
        OutDamageDealt = new OutputPort<int>(() => _damageDealt);
        Ports.Add(0x9F3A_C12B, InMagnitude);
        Ports.Add(0x4D81_77E0, InTarget);
        Ports.Add(0x2C5B_AA09, OutDamageDealt);
    }

    int _damageDealt;   // backing field for OutDamageDealt's reader

    // Mode 2: inherits Execute from CardCommandDispatcher; generator fills BuildPayload / WriteOutputs:
    protected override Strike BuildPayload() => new Strike {
        Magnitude = InMagnitude.Read(),   // returns the wired upstream value, or default(int) if unwired
        Target    = InTarget.Read(),
    };
    protected override void WriteOutputs(StrikeResult result) => _damageDealt = result.DamageDealt;

    // Mode 1 (IExecutable on payload) alternative would replace the two methods above with:
    //   public override ValueTask Execute(CardEffectRunner runner) {
    //       var payload = BuildPayload();
    //       return payload.Execute(runner);   // delegates to IExecutable<TRunner>
    //   }
    // and the class would inherit RuntimeNode<CardEffectRunner> directly, not CardCommandDispatcher.
}
```

Why this works without reflection:

- **`Dictionary<int, Port>` lookup** is O(1) integer-keyed. No string hashing, no per-call allocation.
- **The cast lives once, inside `Connection.Bind`.** It becomes a single `is Port<TFrom>` / `is Port<TTo>` check (plus a future converter lookup) at hydration. `InputPort<T>.Read()` after that is a direct typed delegate invoke — no cast on the read path.
- **Closure allocation** (`() => _damageDealt`) is one heap allocation per output port per node, paid once at construction. The hot path is `InMagnitude.Read()` — direct typed call through the stored `Connection<T>`.
- **Stable port IDs as dict keys** mean a generator change that adds or removes a port in the source order doesn't shift any other port's ID. Existing assets keep wiring correctly. Removed ports become bake-time errors with a clear "port `Magnitude` no longer exists on `Strike`" diagnostic.

#### Re-bake stability

Hydration runs **once per controller**. If a consumer wants multiple independent runtime instances of the same asset (one per card in play, etc.), they construct one controller per instance. The asset is shared and immutable; per-controller state lives in the runtime-only `Connection<T>` slots and any per-instance fields the generator marks `[NonSerialized]`.

Re-baking the asset (designer edits the source file) re-runs `OnImportAsset` and produces a fresh runtime SO. The baker preserves `nodeId`s for unchanged editor nodes (via the `editorGuid → nodeId` map persisted on the previous runtime asset); new nodes get new ints, removed nodes' ints are retired. Port IDs are stable as long as the generator doesn't change a payload's field set. Existing controllers holding the *old* asset keep working until disposed; new controllers built after the re-import use the new asset. There is no in-place mutation of an in-flight controller's tree.

### Typed Runner — generic execution context

Runtime nodes don't take a service interface; they take a **typed runner** the consumer defines. The package supplies a minimal abstract base:

```csharp
// Package — game-agnostic
public abstract class GraphRunner {
    public CancellationToken CancellationToken { get; init; }
}

public abstract class Graph<TRunner> : GraphToolkit.Graph
    where TRunner : GraphRunner { }

// RuntimeNode / RuntimeNode<TRunner> are defined in "Runtime asset format" above.
// Initialization is opt-in via IInitializableNode<TRunner> — see "Lifecycle hooks".
```

The package itself knows nothing about commands, dispatch, or any consumer domain. Hand-written generic helper nodes (Branch, Equals, etc.) are typed `RuntimeNode<TRunner>` and parameterized over the consumer's runner.

Consumer integration adds the services on their own runner subclass and writes helper bases for source-genned nodes to extend:

```csharp
// Consumer — Card Framework integration
public class CardEffectRunner : GraphRunner {
    public IEffectScope EffectScope { get; init; }
    public Player Owner { get; init; }       // host-injected reference, exposed however the consumer prefers
    public CardData Card  { get; init; }
    public T GetService<T>() { ... }         // optional service-locator helper, consumer's call
}

// Editor graph subclass is auto-emitted from [GraphPackage] (see Asset lifecycle section).
// Consumer can extend it via partial class if they need custom OnGraphChanged etc.

// Mode 2 helper base — written once, generator emits typed subclasses per Command.
public abstract class CardCommandDispatcher<TCmd, TResult> : RuntimeNode<CardEffectRunner>
    where TCmd : Command<TResult>, new()
{
    protected sealed override async ValueTask Execute(CardEffectRunner runner) {
        var cmd = BuildPayload();
        var result = await runner.EffectScope.Dispatch(cmd);
        WriteOutputs(result);
    }
    protected abstract TCmd BuildPayload();
    protected abstract void WriteOutputs(TResult result);
}
```

Why the runner is separate from the editor `Graph` subclass: the editor `Graph` is a `ScriptableObject` shared across all instances of an asset. Runtime services like `IEffectScope` are per-execution and can't live on a shared SO. The runner is the per-execution carrier.

The editor `Graph<TRunner>` subclass is generator-emitted from `[GraphPackage]` (see Asset lifecycle), as `partial` so the consumer can extend it without losing the generated bits.

### Executor

```csharp
internal sealed class GraphExecutor<TRunner> where TRunner : GraphRunner {
    public ValueTask           Run(RuntimeNode<TRunner> root, TRunner runner);
    public ValueTask<TReturn>  RunWithReturn<TReturn>(RuntimeNode<TRunner> root, TRunner runner);
}
```

- Pure async tree walk. Each node's `Execute(runner)` may invoke downstream nodes via the executor.
- Cancellation propagates via `runner.CancellationToken`.
- No central scheduler, no middleware list — cross-cutting concerns live on the consumer's runner (services it exposes) and on the framework pipeline its dispatcher nodes feed into.

### Integration with `IEffectScope` (Card Framework adapter)

There is no `IGraphScope` interface in the package. The Card Framework integration creates a `CardEffectRunner` that holds `IEffectScope` as a property; consumer-authored helper bases (e.g., `CardCommandDispatcher`) read from `runner.EffectScope` directly.

For non-Card-Framework consumers, the same pattern applies: subclass `GraphRunner`, add the services you need, write helper bases that bridge package-generated nodes to your domain. The package never imports your domain types.

---

## Package API

### `GraphController<TRunner>`

The controller is generic over the consumer's runner type — the same one that types nodes:

```csharp
public sealed class GraphController<TRunner> where TRunner : GraphRunner {
    public GraphController(GraphAsset<TRunner> asset);

    // One-time setup; walks the runtime tree for IInitializableNode<TRunner> instances
    public void Initialize(TRunner runner);

    // Direct invocation of entry payloads — payload type drives entry lookup.
    // Constraint is `class` because entry payloads can be either
    // IGraphEntry<TRunner> (Mode 1) or descendants of [GraphPackage].EntryBase (Mode 2);
    // both shapes are reference types but don't share a common base.
    public ValueTask        Run<TEntry>(TEntry payload)      where TEntry : class;
    public ValueTask<bool>  Validate<TEntry>(TEntry payload) where TEntry : class;

    // Listener access for the consumer to register/unregister with their pipeline
    public IReadOnlyList<ICommandListener<TRunner>> CommandListeners { get; }

    // Lifecycle: tells listener nodes their subscription state may need refreshing
    public void RefreshListeners();

    public void Dispose();
}

public interface ICommandListener<TRunner> where TRunner : GraphRunner {
    Type CommandType { get; }
    TriggerPhase Phase { get; }
    ValueTask<TriggerResult> Invoke(object command, TRunner runner);
}

public enum TriggerPhase { Before, After }

public abstract class TriggerResult {
    public sealed class Pass    : TriggerResult { public object ModifiedCommand; }
    public sealed class Cancel  : TriggerResult { public string Reason; }
    public sealed class Replace : TriggerResult { public object ReplacementCommand; }
}
```

The runner is supplied by the consumer at `Initialize` time and threaded through the executor on every entry-point invocation. Listener `Invoke` calls also receive the runner so the listener can dispatch follow-up commands through it.

### Consumer-side wiring

The package ships **no** registration policy. Consumer drives it:

```csharp
// Game-side card binding
class CardEffectBinding : MonoBehaviour {
    GraphController<CardEffectRunner> graph;
    CardEffectRunner runner;

    void Awake() {
        runner = new CardEffectRunner {
            EffectScope = framework.EffectScope,
            Owner = ownerPlayer,
            Card  = cardData,
            CancellationToken = lifetime.Token,
        };
        graph.Initialize(runner);
    }

    void OnEnterPlay() {
        foreach (var listener in graph.CommandListeners)
            framework.Pipeline.Register(listener, this.Id);
    }

    void OnLeavePlay() {
        framework.Pipeline.RemoveAll(this.Id);
    }

    async Task PlayCard() {
        if (await graph.Validate(new Play { special = false }))
            await graph.Run(new Play { special = false });
    }
}
```

The consumer's binding code is the only place that knows about zones, ownership, or pipeline mechanics.

---

## Vertical slice — minimum viable consumer

Scenario: a fresh consumer wants a graph with two nodes — `OnPlay` entry → `Log` action — running in their game. Mode 1 (marker interfaces + `IExecutable<TRunner>`) so payloads are self-executing and there's exactly one class per node.

### Files the consumer writes (4 files, ~50 lines)

```csharp
// ─────────────────────────────────────────────────────────────────
// MyGame/Runtime/MyRunner.cs
// Per-execution context. Services-only. No editor/generator metadata.
// ─────────────────────────────────────────────────────────────────
namespace MyGame
{
    public sealed class MyRunner : GraphRunner
    {
        public ILogger Logger { get; init; }
    }
}
```

```csharp
// ─────────────────────────────────────────────────────────────────
// MyGame/Runtime/Payloads.cs
// Concrete node types. Adding a file here = a new node in the editor.
// IGraphEntry<MyRunner> / IGraphAction<MyRunner> = "belongs to MyRunner's package".
// IExecutable<MyRunner> = "this payload executes itself; no DispatcherBase needed".
// ─────────────────────────────────────────────────────────────────
namespace MyGame
{
    public sealed class OnPlay : IGraphEntry<MyRunner>
    {
        public int CardId;     // → output port

        // Entry payloads typically don't implement IExecutable —
        // they're just data fired at the controller. The generated runtime
        // node writes their fields to the output ports and advances flow.
    }

    public sealed class Log : IGraphAction<MyRunner>, IExecutable<MyRunner>
    {
        public string Message; // → input port

        public ValueTask Execute(MyRunner runner)
        {
            runner.Logger.Log(Message);
            return default;
        }
    }
}
```

```csharp
// ─────────────────────────────────────────────────────────────────
// MyGame/AssemblyInfo.cs
// One attribute. Multi-package projects stack multiple of these.
// ─────────────────────────────────────────────────────────────────
[assembly: GraphPackage(
    Runner            = typeof(MyGame.MyRunner),
    Extension         = "mygraph",
    AssetMenu         = "MyGame/Graph",
    Convention        = PortConvention.AllFieldsIn,
    RegistryNamespace = "MyGame.Graph.Generated"
    // No CommandBase / DispatcherBase — Mode 1.
)]
```

```csharp
// ─────────────────────────────────────────────────────────────────
// MyGame/Components/GraphHost.cs
// Game-side wiring. One per gameplay object that uses graphs.
// MyGraphAsset is generator-emitted (see below).
// ─────────────────────────────────────────────────────────────────
namespace MyGame
{
    public sealed class GraphHost : MonoBehaviour
    {
        [SerializeField] MyGraphAsset graphAsset;     // designer drops .mygraph file here

        GraphController<MyRunner> controller;         // package-shipped sealed class
        MyRunner                  runner;

        void Awake()
        {
            runner = new MyRunner
            {
                Logger            = ServiceLocator.Get<ILogger>(),
                CancellationToken = destroyCancellationToken,
            };

            controller = new GraphController<MyRunner>(graphAsset);
            controller.Initialize(runner);
        }

        public ValueTask Play() =>
            controller.Run(new OnPlay { CardId = 42 });

        void OnDestroy() => controller.Dispose();
    }
}
```

That's it. **4 files.** No `Bases.cs`, no `MyGraph.cs`, no `MyGraphAsset.cs`, no `MyGraphImporter.cs`, no separate behavior classes — generator handles all of those, payload self-executes via `IExecutable`.

### What the generator emits (consumer never writes any of this)

#### Per-package trio

```csharp
// AUTO-GENERATED — from [assembly: GraphPackage(Runner = typeof(MyRunner), ...)]
namespace MyGame.Graph.Generated
{
    [Graph("mygraph")]
    public partial class MyGraph : Graph<MyRunner>
    {
        [MenuItem("Assets/Create/MyGame/Graph")]
        static void CreateNew() =>
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<MyGraph>("New Graph");
    }

    public sealed class MyGraphAsset : GraphAsset<MyRunner> { }

    [ScriptedImporter(version: 1, ext: "mygraph")]
    internal sealed class MyGraphImporter
        : GraphAssetImporterBase<MyGraph, MyRunner, MyGraphAsset> { }
}
```

`MyGraph` is `partial` so the consumer can extend it with custom `OnGraphChanged`, additional menu items, etc. without losing the auto-emitted bits. If the consumer hand-writes any of the three (e.g. to bump the importer version), the generator detects the existing declaration and skips emission for that one.

#### Per-payload nodes

```csharp
// AUTO-GENERATED — from OnPlay : IGraphEntry<MyRunner>
namespace MyGame.Graph.Generated
{
    public sealed class OnPlayEditorNode : Node { /* output port: CardId */ }

    public sealed partial class OnPlayRuntime : RuntimeNode<MyRunner>
    {
        public OutputPort<int> CardId;

        public OnPlayRuntime()
        {
            CardId = new OutputPort<int>(() => _cardId);
            Ports.Add(0x4F2A_8B17, CardId);   // FNV-1a("CardId")
        }

        int _cardId;   // backing field — written when the entry payload is bound at dispatch

        public override ValueTask Execute(MyRunner runner) { /* write _cardId from payload, walk flow */ }
    }
}
```

```csharp
// AUTO-GENERATED — from Log : IGraphAction<MyRunner>, IExecutable<MyRunner>
// Decision: payload implements IExecutable<MyRunner> → emit self-executing runtime node.
namespace MyGame.Graph.Generated
{
    public sealed class LogDispatcherEditorNode : Node { /* input port: Message */ }

    public sealed partial class LogDispatcherRuntime : RuntimeNode<MyRunner>
    {
        public string Message;                  // inline default (serialized)
        public InputPort<string> InMessage;     // wired at hydration via Connection.Bind

        public LogDispatcherRuntime()
        {
            InMessage = new InputPort<string>();
            Ports.Add(0x77E1_3C20, InMessage);   // FNV-1a("Message")
        }

        public override ValueTask Execute(MyRunner runner)
        {
            var payload = new Log
            {
                Message = InMessage.Read() ?? this.Message,   // upstream value or inline default
            };
            return payload.Execute(runner);   // delegates to IExecutable.Execute
        }
    }

    // Listener + Return forms emitted alongside (Log is action-shaped) — same pattern.

    public static partial class Registry
    {
        public static void Register(IRegistryBuilder b)
        {
            b.Map<OnPlayEditorNode, OnPlayRuntime>(typeof(OnPlay))
             .Port("CardId",  OnPlayRuntime.Ports.CardId);

            b.Map<LogDispatcherEditorNode, LogDispatcherRuntime>(typeof(Log))
             .Port("Message", LogDispatcherRuntime.Ports.Message);
        }
    }
}
```

### Mode 2 contrast — Card Framework, payloads untouched

```csharp
// MyGame/AssemblyInfo.cs — declares package using the existing Card Framework hierarchy
[assembly: GraphPackage(
    Runner         = typeof(CardEffectRunner),
    Extension      = "card",
    AssetMenu      = "Effects/Card Effect",
    Convention     = PortConvention.CommandResultPair,
    CommandBase    = typeof(Command<>),                      // existing CF base
    DispatcherBase = typeof(CardCommandDispatcher<,>)        // consumer-supplied helper
)]

// Card Framework's payload — no marker interface, no IExecutable, no graph attributes
public sealed record Strike : Command<StrikeResult>
{
    public int    Magnitude;
    public Player Owner;
}

// Consumer's helper base — written once, applies to every Command<,> in the package
public abstract class CardCommandDispatcher<TCmd, TResult> : RuntimeNode<CardEffectRunner>
    where TCmd : Command<TResult>, new()
{
    protected sealed override async ValueTask Execute(CardEffectRunner runner)
    {
        var cmd    = BuildPayload();                          // generator-supplied
        var result = await runner.EffectScope.Dispatch(cmd);  // CF pipeline
        WriteOutputs(result);                                 // generator-supplied
    }
    protected abstract TCmd BuildPayload();
    protected abstract void WriteOutputs(TResult result);
}
```

`Strike` stays a clean Card Framework record. The generator emits `class StrikeDispatcherRuntime : CardCommandDispatcher<Strike, StrikeResult>` and fills in `BuildPayload()` / `WriteOutputs()` from the typed input/output port slots. Same per-package trio (`CardEffectGraph`, `CardEffectGraphAsset`, `CardEffectGraphImporter`) auto-emitted.

### What the package provides

- `GraphRunner`, `Graph<TRunner>`, `RuntimeNode<TRunner>`, `Connection`/`Connection<T>`
- `IGraphEntry<TRunner>`, `IGraphAction<TRunner>`, `IExecutable<TRunner>` (marker + capability interfaces)
- `GraphAsset<TRunner>`, `GraphAssetImporterBase<TGraph, TRunner, TAsset>`
- `GraphController<TRunner>` (sealed class, not a base — instantiated by the consumer)
- `GraphBaker`, `GraphExecutor<TRunner>`
- Built-in generic nodes (`Branch`, `Cancel`, `Replace`, `Return`, `ReturnBool`, predicates, math, conversions) — typed over any consumer's runner
- Source generator (Roslyn incremental) and an attributes-only DLL (`[GraphPackage]`, `[GraphPort]`, `[GraphHidden]`, `[GraphMenu]`, `[In]`, `[Out]`)

### End-to-end runtime trace (Mode 1)

1. **Compile.** Generator runs → emits the per-package trio (`MyGraph`, `MyGraphAsset`, `MyGraphImporter`), per-payload nodes (`OnPlayRuntime`, `LogDispatcherRuntime`), and `Registry`. `GraphHost.cs` references `MyGraphAsset` (now a real type).
2. **Author.** Designer right-clicks in project → `Create / MyGame / Graph` → `Foo.mygraph` opens in Graph Toolkit. Drops `OnPlay`, drops `LogDispatcher`, wires `OnPlay.CardId` → `[ToString]` (built-in) → `LogDispatcher.Message`. Saves.
3. **Bake.** Unity re-imports `Foo.mygraph` → `MyGraphImporter.OnImportAsset` → `GraphBaker` walks the editor graph, assigns stable `nodeId`s (`editorGuid → int` map, persisted on each runtime node), resolves port names to `portId`s through `Registry`, emits a `MyGraphAsset` SO with three runtime nodes and two `ConnectionRecord`s. `ctx.SetMainObject(asset)`.
4. **Reference.** Designer drops `Foo.mygraph` onto `GraphHost.graphAsset` field on a prefab. The reference points at the runtime SO inside the file (the main object).
5. **Hydrate.** Game runs, `Awake()` builds `MyRunner`, constructs `GraphController<MyRunner>(graphAsset)`, calls `Initialize(runner)`. Hydration indexes nodes by id, walks each `ConnectionRecord` calling `to.Bind(toPortId, from, fromPortId)` — the base `RuntimeNode.Bind` looks up both ports through the dict, calls `Connection.Bind(input, output)` (the single cast/conversion seam), stores the typed `Connection<T>` on the input port, and appends it to `to.Connections`.
6. **Run.** `Play()` calls `controller.Run(new OnPlay { CardId = 42 })`. Executor looks up `typeof(OnPlay)` → root node → runs flow:
   - `OnPlayRuntime.Execute` writes `_out_CardId = 42`, advances flow.
   - `ToStringRuntime` reads `_in_Value.Read()` (= 42), writes `_out_Result = "42"`.
   - `LogDispatcherRuntime.Execute` constructs `new Log { Message = "42" }`, calls `payload.Execute(runner)`.
   - `runner.Logger.Log("42")` fires.

That's the complete slice from designer-authored file to runtime side-effect. Consumer surface = 4 files. Everything else is package code, generator output, or designer-authored graph files.

---

## Lifecycle hooks

Two generic interfaces nodes can opt into. They're typed over the same runner the rest of the runtime uses:

```csharp
public interface IInitializableNode<TRunner> where TRunner : GraphRunner {
    void Initialize(TRunner runner);
}

public interface IListenerNode<TRunner> where TRunner : GraphRunner {
    void RefreshSubscriptions(TRunner runner);
}
```

- `GraphController<TRunner>.Initialize(runner)` walks the runtime tree once and calls `Initialize(runner)` on any `IInitializableNode<TRunner>`. Nodes can fetch typed services from the runner here and cache them.
- `GraphController<TRunner>.RefreshListeners()` walks the tree and calls `RefreshSubscriptions(runner)` on any `IListenerNode<TRunner>`. Used when the host wants to re-evaluate listener activity (e.g., on zone change). Actual pipeline registration stays consumer-driven; this hook just updates internal node state.

There is no separate `IGraphHost` interface in v1 — anything the package would have asked from the host (host-injected references, diagnostics, etc.) lives on the consumer's runner subclass directly. The consumer decides what's exposed and how it's named.

---

## Host-injected references (no blackboard in v1)

v1 does **not** ship a designer-managed blackboard. Two paths cover the use cases the blackboard would have served:

1. **Host-injected references** (Owner, Card, Parent, Attachment, etc.) — exposed as typed properties on the consumer's `Runner` subclass (`runner.Owner`, `runner.Card`). Consumer-authored accessor nodes (`[Get Owner]`, `[Get Card]`, …) read from those properties directly. Each accessor is a small `RuntimeNode<TRunner>` subclass written once per reference type.
2. **Designer constants** (Magnitude = 7, EnergyCost = 3, etc.) — live as inline embedded port values on the dispatcher nodes via Graph Toolkit's `port.TryGetValue<T>` mechanism. Less DRY than a shared variable, but acceptable for v1.

What v1 does **not** support:

- A FIXED/MANAGED/VARIABLES panel
- Designer-declared shared variables
- Mutable graph-scope state (counters, accumulators, etc.)
- Generic `[Get Variable]` / `[Set Variable]` nodes

A full blackboard system (whether GT-native or rolled-our-own) is a v2 follow-up. The decision between approaches is deferred until we can spike against the real toolkit's blackboard API.

---

## Validation & error handling

### Edit-time

Implemented via Graph Toolkit's `OnGraphChanged(GraphLogger)` hook on the auto-emitted `<R>Graph : Graph<TRunner>` partial class. The package ships shared validation helpers, and the consumer can extend the partial class to add custom rules; the default-rules implementation lives in the generator-emitted half. Each rule logs to the supplied logger:

- All entry/listener Run flows must end at a valid terminator
- Validate flows must end at `[Return Bool]` (if connected at all)
- Required input ports must be wired or carry a constant
- Hand-written generic nodes provide their own validators
- Generated nodes inherit a base validator with default rules

### Bake-time (ScriptedImporter)

- Detect cycles in flow paths (other than legitimate trigger registration cycles, which aren't graph-internal)
- Validate type compatibility between connected ports
- Emit the generator-produced concrete `GraphAsset<TRunner>` subclass instance only if no errors; on error, log via `ctx.LogImportError` and skip emission (no main object set, source file becomes "edits but doesn't build")

### Runtime

- Executor propagates exceptions; consumer's pipeline catches and decides
- Cancellation via `runner.CancellationToken` aborts the walk cleanly
- Diagnostics / telemetry hooks (if needed) live on the consumer's runner — package defines no event surface in v1

---

## Phasing

The first two milestones deliberately split the design risk from the meta-tooling risk: M0 hand-writes one of every shape so we validate the architecture against runtime + editor + Unity end-to-end **before** writing a Roslyn generator. M1 then builds the generator with M0 as the golden output. If anything in the design (Connection<T>, [SerializeReference] polymorphism, port-ID switches, hydration, nodeId stability) turns out to be wrong, M0 surfaces it cheaply. M1 inherits a known-good template.

### Implementation status (May 2026)

| Milestone | Status | Notes |
| --------- | ------ | ----- |
| **M0** | **Done** | Golden vertical slice under `Assets/GraphFlowM0/` — bake, hydrate, `GraphController`, player smoke; hand-written trio + registry switches in `GraphBaker`. |
| **M1** | **Done** | Trio + payload-driven editor/runtime nodes, registry-driven generic baker, EFG diagnostics, convention strategy abstraction, snapshot harness — see [Recent generator-stabilization work](#recent-generator-stabilization-work-2026-05-03) and [M1 closeout](#m1-closeout-2026-05-03) below. |
| **M2** | **Done** | Generic-node generation pass on the **typed-port-handle model** (port handles + `Connection.Bind` seam, dict-based dispatch on `RuntimeNode` base, `partial void InitializePorts()` for OutputPort assignment, no port-id leakage to user code, pure-data nodes inherit from `RuntimeNode` directly without `TRunner`/`Execute`); minimum built-in set (Branch, Cancel, Return, ReturnBool, Not); Validate/Run flow ports on entry/listener nodes; `OnGraphChanged` validation (EFG-V01..V04); smoke graph extension; sandbox folder rename. `[Return Strike]` deferred to M3 (Mode 2 dependency); multi-T predicates (`Equals<T>` etc.) and remainder of catalog deferred to M4. See "Generic-node emission" subsection (which carries a design-iteration log of the three-pass redesign) and [M2 closeout](#m2-closeout-2026-05-03) below. |
| **M3** | **Done** | Package consolidation, per-run `Flow` state, typed `(TPayload, TResult)` entries unified with triggers, cross-asm `[GraphNode]` discovery, M0 + CardSandbox both demoted into the package as samples. D1–D9 all landed; one structural deviation from the plan default (no-tilde `Samples/` for both sandboxes — see [M3 closeout](#m3-closeout-2026-05-03) below). Sequenced as five phases (skeleton+moves, Flow pivot, generator, CardSandbox rewrite, sandbox demotion) with build-green discipline between commits. |
| M4+ | Planned | Polish + remaining catalog per sections below. |

#### Recent generator-stabilization work (2026-05-03)

End-to-end debugging of the editor compile in Unity surfaced four bugs and one design-constraint clarification:

1. **Mode 2 editor nodes silently dropped.** `GraphPayloadNodeEmitter` was reading `[GraphCommandPair(ResultType = typeof(...))]` via `TypedConstant` against the *referenced* runtime asm. The C# compiler omits the assembly qualifier when serializing same-assembly `typeof()` into attribute metadata, so Roslyn returned `TypedConstant.Kind = Error` cross-assembly and `TryGetCommandPair` silently returned false. `EchoDispatcherEditorNode` was never emitted, breaking `GraphBaker`. Fixed by splitting attribute reading from result-type lookup: when the typeof argument is unresolvable, fall back to scanning the runtime assembly for a `MyDispatcherBase<{Cmd}, ?>` subclass and harvesting the closed generic. Runtime pass still uses the attribute (works because it reads from source).
2. **`Scaffold.GraphFlow.PackageAttributes.dll` collided with the same-named asmdef.** Earlier rename attempts only renamed the file; `dotnet build` regenerated the old DLL because `<AssemblyName>` was unchanged. Now renamed end-to-end: `<AssemblyName>` → `Scaffold.GraphFlow.AttributesLib`, sync script sweeps both legacy filenames, asmdef GUID preserved so consumer references don't break.
3. **`MySmokeGraphAsset` SO failed `AddObjectToAsset`.** Unity's `MonoScript` binding for `ScriptableObject` types requires a real on-disk `.cs` file with a matching name; generator-emitted virtual sources do not satisfy that lookup. **Decision: do not emit the GraphAsset SO from the generator.** The consumer hand-writes a one-line `<Stem>GraphAsset.cs` (the only piece of the trio with this constraint — Graph + Importer are not SOs). Comment in `GraphPackageTrioEmitter` documents why. Roslyn-writes-to-disk and an editor-side AssetPostprocessor were both considered and rejected as more complexity than the one-liner is worth at v1 scale.
4. **Empty graph rejected at bake.** `GraphBaker.Bake` was logging `"Graph has no nodes."` and setting `HasErrors = true`, which broke Unity's "Create new graph" flow (always starts empty). Now an empty editor graph produces an empty `MySmokeGraphAsset` with no error.
5. **Standalone Roslyn host as the only honest debugger.** Unity's Console doesn't show generator output; the editor compile silently produced nothing. A small console app (`Microsoft.CodeAnalysis.CSharp` 4.3.1, mirroring Unity's editor-asm setup) drove the generator end-to-end and let us inspect emitted trees + raw attribute blobs. Now lives as `Generators/Scaffold.GraphFlow.PackageGenerator.SnapshotTests/` — see M1 closeout.

#### M1 closeout (2026-05-03)

Built everything still missing from M1:

1. **Registry infrastructure (package side).** New types in `Assets/GraphFlowM0/Editor/`:
   - `GraphPackageRegistry<TRunner>` — holds per-editor-node-type `NodeRegistration`s (factory delegate, four port-name → port-id dictionaries split by direction × flow/data, optional `EntryTypeId`).
   - `GraphBakerCore` — generic, registry-driven baker. Replaces the per-payload `switch` blocks in the old hand-written `GraphBaker.cs` (deleted). One implementation now serves any `[GraphPackage]` consumer.
   - `GraphAssetImporterBase` — refactored to take a `Registry` property abstract member; the `Bake` method is gone (the base now owns the bake call).
2. **Registry emission (generator side).** New file `Generators/Scaffold.GraphFlow.PackageGenerator/GraphRegistryEmitter.cs`. The trio emitter now produces `<Stem>GraphRegistry.g.cs` in addition to Graph + Importer; `GraphPayloadNodeEmitter` collects per-payload registration snippets via an out-parameter list, and the trio emitter assembles them into a `static partial class <Stem>GraphRegistry` with `RegisterGenerated` (impl) + `RegisterAdditional` (partial hook for consumer-hand-written extras like the M0 `IntToString` data node).
3. **Unified `Ports.FlowIn` for Mode 1.** `EmitExecutableRuntime` previously emitted `public const int FlowInSlotId = 0;` outside the Ports class. Generator now emits `Ports.FlowIn = 0` so editor + runtime + registry all reference port IDs through one symbol shape.
4. **EFG diagnostics.** New `Diagnostics.cs` with descriptors for `EFG005` (command-pair payload with no resolvable result type, after the cross-assembly attribute fallback also fails), `EFG007` (action payload with no execution path — neither `IExecutable` nor `[GraphCommandPair]` nor a viable `DispatcherBase`), and `EFG008` (payload satisfies bindings for two different `[GraphPackage]` declarations). EFG008 is reported once from a cross-package pre-pass in editor mode to avoid duplicate diagnostics.
5. **Convention strategy abstraction.** `Convention` enum value now read off `[GraphPackage]` and stored on `GraphPackageModel`. New `FieldClassifier` splits a payload's fields into input / output buckets per convention. M1 implements `AllFieldsIn` (default — current behaviour) and `AttributedFields` (filters by `[In]`/`[Out]`). `CommandResultPair` is implicit in the Mode 2 emit path (cmd → inputs, result → outputs); `MutableInReadOnlyOut` deferred to M4 alongside the corresponding `EFG002`/`EFG003` diagnostics. The classifier plugs into the IExecutable action emit path; entry / Mode 2 emit are unaffected.
6. **Snapshot tests.** New project `Generators/Scaffold.GraphFlow.PackageGenerator.SnapshotTests/`. Drives the deployed generator DLL against two fixtures (`Scaffold.GraphFlow.M0` runtime, `Scaffold.GraphFlow.M0.Editor` editor) and diffs each emitted tree against a checked-in `Snapshots/{Editor,Runtime}/<name>.g.cs.expected`. Pass `--update` to refresh. Currently baselined at 9 trees (3 runtime + 6 editor).
7. **Analyzer noise.** Generator + snapshot projects now suppress the repo-level SCA style rules that fight string-emission code (long methods, nested calls, expression bodies, line wrapping). Suppression is scoped — application code elsewhere still gets the full ruleset.

`MySmokeGraphAsset` remains hand-written (one line) — Unity's MonoScript binding for ScriptableObject types still requires an on-disk `.cs` file with a matching name, and writing from a generator is the wrong place for that side effect (decided earlier; see #3 in the previous section).

#### M2 closeout (2026-05-03)

Closed out the rest of M2 on top of the port-handle / generic-node pipeline that landed in the prior commits:

1. **Built-in flow terminators.** `Cancel<TRunner>`, `Return<TRunner>`, `ReturnBool<TRunner>` added under `Assets/GraphFlowSandbox/Runtime/Nodes/`. All three are `[GraphNode]`-attributed and pass through the same generator pipeline as Branch / Not — generator emits the default ctor, editor mirror, and registry entry; author surface is just port-handle fields + `Execute`. Result-channel hooks (`Cancelled`, `ReturnValue`) live on `GraphRunner` as a v1 boxed slot; the typed Mode-2 channel (`Return<TCmd>` / `[Return Strike]`) lands in M3.
2. **OnGraphChanged validation rules (EFG-V01..V04).** Generator now emits two helper methods alongside the duplicate-entry check: `ValidateEdgePairings` (errors when an edge pairs flow↔data or hits an unknown port name — EFG-V03) and `ValidateRunFlowTerminates` (warns when a flow output is unwired and the owning node isn't itself a terminator, i.e. has zero `FlowOutputPortIds` — EFG-V04). Type-mismatch (V06) is preempted by GraphToolkit's UI-level port wiring filter; required-input-unwired (V05) needs a `[Required]` metadata seam not yet designed and is deferred to M4.
3. **Smoke runtime tests for Branch + Not + Return + Cancel + ReturnBool.** Three new tests in `M0SmokeRuntimeTests` exercise the True path through Return, the False path through Cancel, and the result-write path through ReturnBool — pinning the generator-emitted port ids inline as the existing tests do.
4. **Sandbox folder rename.** `Assets/GraphFlowM0/` → `Assets/GraphFlowSandbox/`. Asmdef/namespace identities (`Scaffold.GraphFlow.M0.*`) are unchanged — only the on-disk path moves, so consumer references and the snapshot fixtures' assembly-name lookup keep working.
5. **Snapshots.** `.expected` files for the three new generic nodes (runtime + editor mirrors) and the refreshed `MySmokeGraphRegistry` / `MySmokeGraphValidation` are checked in. The snapshot harness is a Windows-host program (it loads the Unity-built `Scaffold.GraphFlow.M0.dll` from `Library/ScriptAssemblies/`); rerun with `--update` after the next Unity compile if symbol-walk order shifts.

#### M3 closeout (2026-05-03)

Sequenced into five build-green phases over a single day. Each phase ended at a clean Unity batchmode compile before the next started; agents handled the bulk-mechanical steps (1, 6, 8, 9 below) while the design-bearing decisions and inter-phase coherence stayed in the orchestrating session.

1. **Phase 1 — package skeleton + runtime/editor moves (steps 1, 2, 5).** New `Assets/Packages/com.scaffold.graphflow/Runtime/` + `/Editor/` asmdefs (the existing AttributesLib relocates into `Runtime/Attributes/` so engine-using runtime types can take the root; GUIDs preserved across the move). 10 runtime + 5 editor types relocate from the M0 sandbox, namespace `Scaffold.GraphFlow.M0[.Editor]` → `Scaffold.GraphFlow[.Editor]`. The `using Scaffold.GraphFlow.M0` references in the still-in-place sandbox source files become `using Scaffold.GraphFlow;`. Two follow-up fix commits closed the surface area the bulk-move missed: (a) consumer asmdef references for `Scaffold.GraphFlow.M0.Tests` + both CardSandbox asmdefs needed the new package runtime asmdef GUID, plus 9 CardSandbox source files needed `using Scaffold.GraphFlow;`; (b) generator emit constants and emitted `using` directives needed updating from the M0 namespace to the new package namespace, plus the generator DLL had to be re-synced into Unity. Lessons captured: when files move out of an asmdef, every consumer asmdef and every consumer source file that referenced them needs review (this brief gap was the cause of all 282 errors after phase 1 first landed).
2. **Phase 2 — Flow + Execute pivot (steps 3, 4).** New `Flow` + `FlowOutcome` per D2 — exact public shape from the plan, with `IEffectScope?` deferred to phase 4 (where CardSandbox actually surfaces the need). `RuntimeNode<TRunner>.Execute` signature changes from `Task<FlowContinuation>(TRunner)` to `Task(TRunner, Flow)` per D3; `GraphExecutor.RunFlow` returns `Task<Flow>` and constructs the per-run `Flow` once. `GraphRunner.Cancelled` and `.ReturnValue` removed per D4. Built-in flow nodes (`Branch<TRunner>`, `Cancel<TRunner>`, `Not`, `Return<TRunner, TResult>`) move from the M0 sandbox into the package's `Runtime/Nodes/` (`Scaffold.GraphFlow.Nodes` namespace), with hand-authored ctors + ports-dict population since the M2 generator's same-asm walk doesn't yet discover them in the package; `ReturnBool` deleted. `FlowContinuation.cs` and the `FlowOut` struct deleted (vestigial — built-in nodes now write `flow.GoTo(portId)` directly). M0 sandbox tests migrate to read `flow.Outcome` / `flow.ReadResult<T>()` instead of `runner.Cancelled` / `runner.ReturnValue`. The phase-2 agent also pulled forward minimal Execute-signature emit in the generator (entry + dispatcher emit) so Unity stayed compileable into phase 3.
3. **Phase 3 — generator pivot (step 6).** Concepts.cs surgery per D5: `IGraphEntry<TPayload, TResult>` (and 1-arg `IGraphEntry<TPayload>` sugar inheriting from the 2-arg base) plus `IGraphTrigger<TEvent>` and a `Unit` value-type, all in the package runtime (D5's "in attributes asm" line is incompatible with AttributesLib's no-engine-references stance — the brief made the explicit decision to keep markers in runtime). `EntryRuntimeNode` promoted to `<TEntry, TRunner, TResult>` with `BindRunner` + `Run(TEntry)` per D5. `GraphController` exposes both `Run<TEntry, TResult>(payload) → Task<TResult>` (D9 production API) and `RunFlow<TEntry>(payload) → Task<Flow>` (diagnostic API for tests that need `Outcome` access — judgment call beyond what the plan specs). `EntryNodes : IReadOnlyList<RuntimeNode>` exposed; `Initialize` reflectively binds each entry's typed `Run` closure. D6 cross-asm walk implemented in `EmitGenericNodeArtifacts` — walks every asm referencing `Scaffold.GraphFlow` plus the runner's own asm; runtime partial emission gated on "type lives in current compilation" so consumer asms don't get empty `partial class Branch<>`. After this lands, the M0 sandbox's `MySmokeGraphRegistry.g.cs` automatically includes registrations for `Branch<MySmokeRunner>`, `Cancel<MySmokeRunner>`, `Not` — the package built-ins light up for any consumer with no ceremony. EFG-V07 (one-TResult-per-graph) emitted into the validation file. Snapshot harness's hardcoded paths replaced with repo-relative resolution + `SCAFFOLD_GRAPHFLOW_DLLS` env override; baselines regenerated. M0 sandbox payloads migrated from `IGraphEntry<MySmokeRunner>` to `IGraphEntry<OnPlay>` (etc.); CardSandbox patched minimally to keep compiling. Multi-T `Return<R, TResult>` registry expansion deferred to M4 (matches the plan's M4 multi-T scope) — tests construct `Return<MySmokeRunner, bool>` directly and graph assets that need it can land alongside the M4 work.
4. **Phase 4 — CardSandbox rewrite + IEffectScope (step 8 + the phase-2 deferral).** The M2-prep `CommandPipeline` + `ICommandListener<TCmd, TResult>` model deleted; cross-card command modification now goes through events + trigger entries via D8's pattern. `Flow.Scope` typed as a new empty package-side `IEffectScope` marker interface (a sample-side `ICardEffectScope` inherits from it and adds the actual contract dispatchers use — keeps the package decoupled from any specific scope shape). `GraphController.Initialize` gains an optional `Func<IEffectScope?>?` scope-factory parameter; `GraphExecutor.RunFlow` propagates it onto `flow.Scope`. CardSandbox rebuilt: `CardEffectRunner` holds just the `EventBus` (long-lived host service); per-run state lives on `Flow.Scope`. New `EventBus` stand-in (sample-only — the package doesn't ship an event bus) with sequential `await Publish<T>` / `Subscribe<T>(Func<T, Task>)`. Two cards (hand-authored, no `.gfasset` files): `Strike500` (OnPlay entry → `DealDamageCommand` dispatcher with Amount=5) and `PlusOneDamage` (trigger entry on `PreDamageDealtEvent` that mutates `Amount += 1`). Two tests demonstrate the model: solo Strike500 → 5 damage; Strike500 + PlusOneDamage with triggers wired via the `EntryNodes` catalog pattern-match → 6 damage. Same outcome the M2-prep pipeline-based test produced, achieved through the unified entry-and-trigger model.
5. **Phase 5 — sandbox demotion (step 7) + cleanup.** M0 sandbox moves from `Assets/GraphFlowSandbox/` to `Assets/Packages/com.scaffold.graphflow/Samples/M0Sandbox/`. **Decision: no-tilde `Samples/`, not the plan's default `Samples~/`.** Rationale: the M0 sandbox is currently load-bearing for daily dev workflow — `M0SmokeRuntimeTests` is our primary test suite, the snapshot harness loads `Scaffold.GraphFlow.M0.dll` as a metadata reference, and the `.gfmsmoke` editor asset + player-smoke build script need to compile in the regular project context. `Samples~` would require either rewriting the harness to build M0 source via Roslyn or sample-importing on every test run — neither earns its complexity for M3. Asmdef names + GUIDs preserved across the move so consumer references and the harness's `Library/ScriptAssemblies/` DLL path keep working unchanged. The `.gfmsmoke` graph asset moved into `Samples/M0Sandbox/Smoke/` alongside the player-smoke scene. A small follow-up commit applied the same logic to CardSandbox (originally landed at `Samples~/CardSandbox/` in phase 4): moved to `Samples/CardSandbox/` no-tilde so its 2 tests run on every Unity batchmode pass, and removed the stray pre-existing `Assets/GraphFlowM0/` empty directory + meta. `package.json` `samples[]` array advertises both samples to Package Manager.

**Future cleanup tracked for M4+** (out of scope for M3): split the load-bearing pieces — extract `M0SmokeRuntimeTests` into the package's own `Tests/` folder against synthetic fixtures (no real `MySmokeRunner` needed), then demote the rest of M0 to `Samples~` proper. This honors the plan's "samples don't auto-compile for consumers" intent while keeping the test loop tight today.

**Validation across the milestone.** Each phase ended at: (1) `dotnet build Generators/` Release exit 0; (2) `dotnet run` snapshot harness `OK: all snapshots match.`; (3) Unity batchmode compile clean (zero `error CS####` lines). Phase 4 also ran `-runTests -testPlatform EditMode` and confirmed the new CardSandbox tests pass alongside all 5 M0 tests. The 7 unrelated test failures in `Scaffold.CloudCode` / `Scaffold.LiveOps` are pre-existing baseline (predating M3) — not regressions.

#### Post-M3 polish closeout (2026-05-04)

After M3 closed, hands-on use of CardSandbox (visual authoring) surfaced 9 problems that didn't match earlier discussions. Catalogued in [PostM3-FollowUps.md](PostM3-FollowUps.md) (decisions) and [PostM3-ImplPlan.md](PostM3-ImplPlan.md) (six sequenced phases). Net effect: the framework's user-facing surface is meaningfully cleaner and the runtime drops several layers of incidental complexity introduced during M3. All seven decisions implemented; all 7 GraphFlow tests still green; 14 snapshot baselines stable.

1. **Phase 1 (`9b42a63`) — Marker collapse + drop TResult + remove controller reflection.** D5's `IGraphEntry<TPayload, TResult>` + sugar + `IGraphTrigger<TEvent>` collapse to a single non-generic `IGraphEntry` marker. `Unit` struct deleted. `EntryRuntimeNode` drops TResult (becomes `EntryRuntimeNode<TEntry, TRunner>`). `GraphController.Run<TEntry, TResult>` deleted; `RunFlow<TEntry>` renamed to `Run<TEntry>(payload, ct) → Task<Flow>` — single Run API. Reflection (`MakeGenericMethod` + `MethodInfo.Invoke`) in controller replaced with generator-emitted typed bridges via `EntryRuntimeNodeBase<TRunner>.CreateBridge`. Zero reflection on hot or hydration paths now. Sandbox payloads migrated to non-generic marker.
2. **Phase 2 (`edfefa5`) — String port IDs + two-tier RuntimeNode hierarchy.** Decision #4: `ConnectionRecord` / `FlowEdge` port IDs change from `int` to `string` (field names). `RuntimeNode.Ports` becomes `Dictionary<string, Port>`. `Flow.GoTo(string)`. All `unchecked((int)0x...u)` literals deleted from user code AND built-in nodes — field name IS the port ID. Asset schema bumps to v3. Decision #5: `RuntimeNode` (non-generic) gains `virtual Task Execute(Flow)`; built-in `Branch`, `Cancel`, `Return<TResult>`, `Not` reparent here (non-generic, runner-agnostic). `RuntimeNode<TRunner>` survives for typed dispatchers; gets `BindRunner` + sealed Execute override. `Flow.Runner` added (mirror of Scope). `EntryRuntimeNode` drops TRunner (becomes `EntryRuntimeNode<TEntry>`). Custom dispatchers cast `flow.Runner` if needed. Built-in node registrations no longer close per-runner — one Branch registration per package, ever.
3. **Phase 3 (`4543dd8`) — `OnTrigger<TEvent>` primitive + dynamic-options editor.** Folds problems #7 (Pre/Post node duplication), #8 (single OnEvent picker entry), and the trigger half of #3. New built-in `OnTrigger<TEvent> : EntryRuntimeNode<OnTrigger<TEvent>>` with `Event` payload + `Timing` field. New `Timing` enum (Before/After). New `[GraphEvent]` attribute. Hand-written `OnTriggerEditorNode` in package Editor uses GraphToolkit's `OnDefineOptions` + `OnDefinePorts` to dynamically expose the event type dropdown (string AQN — `TypeReference` deferred) + Timing dropdown. Per-package generator emit gains an `EventTypes` table built from `[GraphEvent]` walk. Hydration constructs `OnTrigger<T>` reflectively (one-time, hydration only). CardSandbox migration: `PreDamageDealtEvent` + `DamageDealtEvent` collapse into one `[GraphEvent] DamageDealt`; `PlusOneDamage` uses `OnTrigger<DamageDealt>` with `Timing.Before`; sample's bus publishes the single event twice with Timing.
4. **Phase 4 (`7eacac6`) — Drop `[GraphCommandPair]`.** Decision #2. Mode-2 commands now discovered by walking the package's `CommandBase` open generic; TResult read from the closed `Command<TResult>` base via Roslyn directly. Attribute deleted. `EFG005` dropped (no anchor); `EFG007` rephrased. `FindResultTypeFromDispatcherSubclass` (the M1-era cross-asm `typeof()` fallback) deleted. M0 sandbox migration: new `MyCommand<TResult>` base; `Echo : MyCommand<FakeResult>, IGraphAction<MySmokeRunner>`; `CommandBase = typeof(MyCommand<>)` added to both M0 AssemblyInfo declarations. CardSandbox `DealDamageCommand` simplifies to bare `: Command<Unit>`.
5. **Phase 5 (`a6a95ea`) — CardSandbox file hygiene.** Decision #6. Sample restructured per the decision's tree: `Events/DamageDealt.cs`, `Entries/OnPlay.cs`, `Commands/DealDamageCommand.cs`, `Cards/Strike500.cs` + `Cards/Strike500Dispatcher.cs`, `Cards/PlusOneDamage.cs` + `Cards/PlusOneDamageMutator.cs`. Static-class-as-namespace wrappers around tangled type webs gone. `Strike500.OnPlayEntry` deleted (BuildAsset uses generator-emitted `OnPlayRuntime`); `Strike500.DealDamageDispatcher` extracted from nested form to top-level `Strike500Dispatcher`. Per-card effect nodes stay hand-authored because the generator-emitted Mode-2 dispatcher needs a constant-int primitive (M4+) to be configurable from `BuildAsset()`.

**Decision #7 (NaughtyAttributes inspector noise on `.gfmsmoke` / `.card` selection)** — accepted as live-with-it. Third-party bug; can't fix from inside the package without overriding ScriptableObject globally (destructive). Editor warnings only, no functional impact. Documented as known papercut.

**M3-deferred follow-up still open** (carried from M3 closeout): split load-bearing M0 pieces — extract `M0SmokeRuntimeTests` into the package's own `Tests/` against synthetic fixtures, then demote the rest to `Samples~`. Out of scope for the post-M3 polish; tracked for the M4 phase.

### Milestone 0 — Hand-written vertical slice (1.5–2 weeks)

**Goal:** prove the architecture compiles, links, bakes, hydrates, and executes end-to-end with no source generator anywhere in the loop. Real `.ext` file on disk → real bake → real ScriptableObject asset → loaded in a player build → `controller.Run` → assert side effect.

The hand-written code must be **representative**, not corner-cut. Validation hinges on the slice being the real shape:

- Project structure: package layout, asmdefs, package manifest. Attributes-only DLL is empty/stubbed.
- Package runtime base classes: `GraphRunner`, `RuntimeNode<TRunner>`, `Connection`/`Connection<T>`, `IGraphEntry<TRunner>`, `IGraphAction<TRunner>`, `IExecutable<TRunner>`, `IInitializableNode<TRunner>`, `IListenerNode<TRunner>`.
- Package asset/import: `GraphAsset<TRunner>` (with **`connections` + `flowEdges`**), `GraphAssetImporterBase<TGraph, TRunner, TAsset>`, `GraphBaker` (real implementation: walks nodes/edges, assigns stable nodeIds, resolves port names → port IDs, emits **`ConnectionRecord`s (data) and `FlowEdge`s (flow)**, validates).
- Package execution: `GraphExecutor<TRunner>`, `GraphController<TRunner>` (sealed).
- A test consumer (smoke project): one runner + one entry payload + one action payload, plus the per-package trio (`<R>Graph`, `<R>GraphAsset`, `<R>GraphImporter`) and per-payload nodes — **all hand-written**.
- The runtime nodes have the real shape: `static class Ports` with `const int` IDs, `[NonSerialized] Connection<T>` slots, `BindInput` / `GetOutputConnection` switches with the `Delegate → Connection<T>` casts. No "I'll just call setters directly for now."
- The asset is a real `[SerializeReference] List<RuntimeNode<TRunner>>` SO stored as a sub-asset via `ctx.SetMainObject`.
- The connection records are the real four-int **data** form `(fromNodeId, fromPortId, toNodeId, toPortId)`; flow ordering uses **`FlowEdge`** separately.
- Hydration does the real two-virtual-call wiring for **data** edges only: `from.GetOutputConnection(portId)` then `to.BindInput(portId, conn)`. Flow uses `flowEdges` + `Execute` → `FlowContinuation`.
- End-to-end test: file on disk → reimport → load asset in **a built player** (not just edit-mode) → `controller.Run` → assert side effect. Validates the build path, not just the editor path.

**Discipline note:** the temptation during M0 will be to keep adding payloads manually because it works. The line is sharp — exactly one of each shape (one entry, one action via `IExecutable`, one action via `DispatcherBase` if we want to validate Mode 2 too), then stop and start M1.

### Milestone 1 — Source generator parity (1.5–2 weeks)

**Bootstrap (started):** attributes and incremental generator projects build under `Generators/Scaffold.GraphFlow.Attributes/` and `Generators/Scaffold.GraphFlow.PackageGenerator/` (`dotnet build` Release). Unity wiring and emitted parity with `Assets/GraphFlowM0/` still to do.

**Goal:** the generator emits exactly what M0 wrote by hand. Delete the M0 hand-written nodes; generator produces them. M0's end-to-end test still passes unchanged.

- Attributes-only DLL: `[GraphPackage]`, `[GraphPort]`, `[GraphHidden]`, `[GraphMenu]`, `[In]`, `[Out]`. `PortConvention` enum.
- Roslyn incremental generator skeleton: read `[assembly: GraphPackage]`, partition payloads by runner, locate base-type / marker-interface descendants.
- Per-package emission: `<R>Graph` (partial), `<R>GraphAsset`, `<R>GraphImporter` trio. Skip emission if the consumer hand-wrote one.
- Per-payload emission: editor `Node` subclasses (Dispatcher / Listener / Return for actions; one Entry node for entries).
- Per-payload runtime emission (M1 form): `static class Ports` constants, `[NonSerialized] Connection<T>` slots, `BindInput` / `GetOutputConnection` switches. (M2 migrates this to typed port handles + `Ports` dict + `Connection.Bind` seam — see M2 milestone.)
- Per-payload execution path: `IExecutable<TRunner>` detection → self-executing `Execute`; otherwise `DispatcherBase` close + `BuildPayload`/`WriteOutputs` emission.
- Registry partial-class emission (editor type → runtime type + port-name → port-ID).
- Conventions: `CommandResultPair` and `AttributedFields` (the two that exercise the strategy abstraction).
- Diagnostics: `EFG005` (missing result pair), `EFG007` (no execution path), `EFG008` (multi-package binding).
- Snapshot tests: take the M0 hand-written file, run the generator on the corresponding payload, diff against the hand-written file. Generator is correct iff diff is empty.

### Milestone 2 — Editor authoring (1.5 weeks)

**Goal:** Generic nodes exist and work in graphs end-to-end. Author surface is typed port handles + `Execute` body; the generator owns ctor + dict population + editor mirror + registry entry; the cast/conversion logic lives once in `Connection.Bind`. See "Generic-node emission (hand-written runtime, generator-completed partial)" in the Source generator section for the full design + iteration history.

- **Runtime support types.** `InputPort<T>`, `OutputPort<T>`, `FlowOut`, `Connection` / `Connection<T>` (the conversion seam), `Port` base. New types live alongside the existing M0 runtime; existing `Connection<T>`-based payload runtime emit migrates to use them through the same emitter changes (ports get a `Read()` API; payload runtime ctors populate the `Ports` dict). Two-stage type hierarchy: `RuntimeNode` (data nodes — no `TRunner`, no `Execute`, just ports + `Bind`); `RuntimeNode<TRunner>` (flow-bearing nodes — adds `Execute`).
- **Attribute set in AttributesLib.** Only `[GraphNode]` (with optional `Category`). The earlier `[Input]` / `[Output]` / `[FlowIn]` / `[FlowOut]` attributes from the first redesign pass are dropped — superseded by the typed port-handle fields. `[GraphPort(Id = N)]` survives for explicit port-id pinning across renames.
- **Generic-node generation pass (new emitter).** `GraphGenericNodeEmitter` walks `[GraphNode]`-attributed classes, emits three artifacts per package: (1) **runtime partial** (`<Name>.g.cs`) — ctor that constructs typed port fields, calls user's `partial void InitializePorts()` body when the class has `OutputPort<T>` fields, populates the `Ports` dict; (2) **editor mirror** (`<Name>EditorNode.g.cs`) — same shape as today (port-name consts + `OnDefinePorts`); (3) **registry entry** — closes the open generic at the package's runner if needed (`new Branch<MySmokeRunner>()`); pure data nodes get an unparameterized factory (`new Not()`). Reuses the shared port-id derivation, registry composer, and snapshot harness from M1.
- **Registry factory return-type relaxation.** `GraphPackageRegistry<TRunner>.NodeFactory` returns `RuntimeNode` instead of `RuntimeNode<TRunner>`. Pure data nodes register directly. The executor still walks `flowEdges` and only calls `Execute` on `RuntimeNode<TRunner>` instances; data nodes are read-only sinks for `Connection`s, never invoked.
- **Diagnostics.** EFG010 (OutputPort field with no `InitializePorts` body assigning it). EFG011 (multi-type-parameter `[GraphNode]` class — emit warning, skip emission, defer to M4).
- **Built-in generic nodes (minimum set for M2).** Flow-bearing: `Branch`, `Cancel`, `Return`, `ReturnBool`. Pure data: `Not`. Remainder of the catalog (Replace, GreaterThan / LessThan, And / Or, math, conversions, `Equals<T>` and other multi-T predicates) deferred to M4 polish — add on demand as graphs need them.
- **Validate / Run flow ports on generated entry / listener nodes.** Generator change to emit two flow-out ports on entry/listener editor nodes; bake honors both as separate flow edges; `EntryRuntimeNode` exposes both port IDs. Snapshots bumped.
- **`OnGraphChanged` edit-time validation.** Initial rule set: required-input-unwired, type-mismatch, dangling flow edges, duplicate entries, every Run flow terminates (Return / Cancel). Per-rule diagnostic IDs reserved under the `EFG` series alongside compile-time ones.
- **Smoke graph extension.** `MySmokeGraph` exercises Branch + Not + Return + Validate flow path. The existing M0 `IntToString` migrates from `RuntimeNode<MySmokeRunner>` (with no-op `Execute`) to `RuntimeNode` (no `Execute`) — proves the data-node hierarchy split end-to-end. Snapshot fixtures updated.
- **Folder rename (last cosmetic commit).** `Assets/GraphFlowM0/` → `Assets/GraphFlowSandbox/` to make it explicit this is the throwaway test bed; final package home is `Assets/Packages/Scaffold.GraphFlow/`. Done last so the substantive commits don't drown in meta-file churn.
- (Deferred:) `[Return Strike]` typed terminator depends on Mode 2 / `ReturnPayloadNode<TCmd>` — lands with the Card Framework integration in M3.
- (Deferred:) Multi-type-param predicates (`Equals<T>`, `IsOfType<T>`) — each closed `T` needs a separate editor node; M4 spec'es the registration shape.
- (Deferred:) `Connection` type-conversion mechanism — architectural seam is in place at `Connection.Bind` for M2; the actual converter registry + conversion logic is M4+.
- (Deferred:) Blackboard panel — v2.

### Milestone 3 — Package consolidation, typed entries, Mode-2 sample (3 weeks)

> **Reframing note (2026-05-03).** The original M3 was scoped narrowly as "Card Framework integration". A design-pass while standing up the CF stand-in surfaced four structural decisions that subsume what the original M3 was trying to do; this milestone now folds them. The CF integration becomes a sample — useful for validating the Mode-2 path end-to-end, not a hard dependency the framework integrates against. Expect M3 to be substantively bigger than M0–M2 because it touches every Execute signature in the system; estimate is widened from 1 week to 3.

> **Supersession map — read this before treating earlier sections as authoritative.** Several earlier sections of this plan (Architecture overview, Concept layer, Source generator, Runtime, Authoring surface) describe shapes from the M0–M2 era. M3 supersedes them in the following ways. When an earlier passage and a D-decision below conflict, **the D-decision wins.**
>
> | Earlier-section claim | Status under M3 | Superseded by |
> | --- | --- | --- |
> | Core types live under `Assets/GraphFlowM0/` (later `Assets/GraphFlowSandbox/`) and namespace `Scaffold.GraphFlow.M0`. | Moved into `Assets/Packages/com.scaffold.graphflow/Runtime/` and `/Editor/`; namespace `Scaffold.GraphFlow`. M0 sandbox demoted to `Samples~/`. | **D1** |
> | `Execute` returns `Task<FlowContinuation>` (or `ValueTask<FlowContinuation>`); the executor reads the `FlowContinuation` to pick the next port. `FlowContinuation.Stop` / `FlowContinuation.Next(portId)`. | `Execute` returns plain `Task` and takes `(TRunner, Flow)`. Routing decisions move onto `Flow` via `GoTo` / `Stop` / `Return` / `Cancel`. `FlowContinuation` is deleted. | **D2 + D3** |
> | `GraphRunner` carries per-run state (`Cancelled`, `ReturnValue` from M2). | Both fields removed. State lives on `Flow.Outcome` / `Flow.Result` for the duration of one Run; runner is reused across runs and stays clean. | **D2 + D4** |
> | `EntryRuntimeNode<TEntry, TRunner>`. `IGraphEntry<TRunner>` marker. Listener nodes are a separate concept (`IListenerNode<TRunner>`). | `EntryRuntimeNode<TEntry, TRunner, TResult>` with three type parameters. `IGraphEntry<TPayload, TResult>` (and the void-sugar `IGraphEntry<TPayload>`). `IGraphTrigger<TEvent>` is a marker subset of entries — *triggers are entries*; there is no separate listener concept on the runtime. | **D5** |
> | `ReturnBool` is a built-in flow terminator. | Removed. Replaced by the typed `Return<TRunner, TResult>` built-in (any `TResult`). The M2 `ReturnBool<TRunner>` becomes `Return<TRunner, bool>`. | **D3 (Return) + D5** |
> | `[GraphPackage]` exposes `EntryBase`, `ListenerBase`, `ReturnBase` knobs. | All three knobs removed. Only `DispatcherBase` and `CommandBase` survive (still load-bearing for Mode-2 emission). | **D7** |
> | Generator walks only the runner's containing assembly for `[GraphNode]` types ("M2 supports a single source assembly per package; consumer-authored nodes in other referenced assemblies are a v2 follow-up"). | Generator walks every asm referenced by the runner's asm that itself references the GraphFlow runtime asm. Built-in nodes in the package light up automatically; shared node libraries supported. | **D6** |
> | `CommandPipeline` + `ICommandListener<TCmd, TResult>` model in CardSandbox (M3-prep `b657389`). | Deleted. Cross-card command modification happens through events + trigger entries (D8). The CardSandbox sample is rewritten on top of D9's entry catalog. | **D8** |
> | Card Framework (Overknights) is referenced as the integration target; M3 is "the CF integration". | Card Framework is **not** integrated. The CF concepts (commands, listeners, scope) are reproduced in a self-contained `CardSandbox` sample under `Samples~/CardSandbox/` purely to validate the Mode-2 path end-to-end. No third-party CF dependency is taken. | **D8 + sample lifecycle** |
> | `controller.Run<TEntry>(payload) → Task` (untyped result). | `controller.Run<TEntry, TResult>(payload) → Task<TResult>`. Each entry node also exposes `Task<TResult> Run(TEntry)` directly so hosts can pattern-match the entry list and invoke without restating type args. | **D5 + D9** |
> | Listener registration / discovery is a runtime concern. | Subscription is host territory. Framework exposes `controller.EntryNodes : IReadOnlyList<RuntimeNode>`; the host pattern-matches typed entries and wires them into whatever event source it owns. No descriptor classes, no listener registry. | **D8 + D9** |
>
> Earlier text that's not in this table is still accurate (port handles, `Connection.Bind` seam, dict-based dispatch on `RuntimeNode`, generic-node emission shape, validation rules EFG-V01..V04, etc. — all from M2 and unchanged in M3).

#### Goal

Promote GraphFlow from "sandbox proving the architecture" to "consumable Unity package". By the end of M3, **every type required to author and run a graph lives inside `Assets/Packages/com.scaffold.graphflow/`**, the runtime is reentrant-safe via per-run `Flow` state, entries are typed end-to-end on both payload and result, triggers are unified with entries, and a CardSandbox sample validates the Mode-2 pipeline against a recreation of `500 Strike`. The M0 sandbox (currently `Assets/GraphFlowSandbox/`) is demoted to a sample (or deleted) — nothing outside the package may be load-bearing.

#### Architectural decisions locked in by M3

These are settled. The implementer should not relitigate; they should implement.

##### D1 — Package is the only home of the framework

- Everything required to run a graph at runtime lives in `Assets/Packages/com.scaffold.graphflow/Runtime/`. This includes: `GraphRunner`, `RuntimeNode`, `RuntimeNode<TRunner>`, `Connection` / `Connection<T>`, `Port` / `InputPort<T>` / `OutputPort<T>` / `FlowOut`, `Flow`, `FlowOutcome`, `EntryRuntimeNode<TEntry, TRunner, TResult>`, `GraphAsset<TRunner>`, `GraphController<TRunner>`, `GraphExecutor<TRunner>`, the lifecycle interfaces, plus the built-in nodes (`Branch`, `Cancel`, `Not`, `Return<TRunner, TResult>`, `ReturnBool` if kept — see D3 / built-ins).
- Editor-side core lives in `Assets/Packages/com.scaffold.graphflow/Editor/`: `GraphPackageRegistry<>`, `GraphBakerCore`, `GraphAssetImporterBase`, `EditorNodeIdentity`. The generator's hardcoded `EditorRegistryNamespace` (currently `"Scaffold.GraphFlow.M0.Editor"` in `GraphRegistryEmitter.cs`) is updated to the package namespace (`"Scaffold.GraphFlow.Editor"` or whatever lands).
- `Generators/` stays where it is (compiled to DLL, synced into the package per `Generators/Scaffold.GraphFlow/sync-unity-dlls.ps1`). This is the one exception to "core lives in the package" — the generator is build-time tooling, not runtime, and Roslyn analyzers ride into Unity through the package's `.meta`-labelled DLL anyway.
- Cross-package dependencies (Unity GraphToolkit, the AttributesLib DLL, Unity engine asms) are fine and expected.
- **No core type may live in a sample asm.** If something currently in `Assets/GraphFlowSandbox/` is needed for the framework to function, it migrates. If it's only there to prove a flow end-to-end, it stays a sample.
- **Namespace migration.** Core types currently live under `Scaffold.GraphFlow.M0`. They move to `Scaffold.GraphFlow` (or `Scaffold.GraphFlow.Runtime` if we want a sub-namespace — implementer's call, but be consistent). The `M0` infix only made sense while the sandbox was the framework; it doesn't anymore.

##### D2 — Runner is reused across runs; per-run state lives on `Flow`

This was the load-bearing pivot. It changes how Execute is shaped.

- **`GraphRunner` is session-long.** It carries services + listener pipelines + scope factories + host references. It does **not** carry result slots, cancellation per-run state, or anything that varies between two `controller.Run` invocations. A controller may be `Run`-invoked many times (a card played multiple times, a trigger firing repeatedly); the same runner instance is reused.
- **`Flow` is the per-run state object.** A new `Flow` is constructed at the start of each `controller.Run`, plumbed through every `Execute` call along the walk, and discarded when the walk terminates. Two concurrent `Run` calls produce two `Flow` instances and never share state.

**`Flow` shape (in `Assets/Packages/com.scaffold.graphflow/Runtime/Flow.cs`):**

```csharp
public enum FlowOutcome { Stopped, Returned, Cancelled }

public sealed class Flow
{
    public CancellationToken CancellationToken { get; }
    public IEffectScope? Scope { get; internal set; }   // IEffectScope is package-level; Mode-2 runners (e.g. CardEffectRunner) populate it; Mode-1 runners can leave it null.
    public string? Reason { get; set; }                  // free-form trace string for debug

    public FlowOutcome Outcome { get; private set; }
    internal object? Result { get; private set; }
    int? _nextPortId;

    // Control + state primitives. All four return Task.CompletedTask after mutating Flow,
    // so authors can write `return flow.GoTo(X);` for one-line Execute bodies.
    public Task GoTo(int outFlowPortId)  { _nextPortId = outFlowPortId; return Task.CompletedTask; }
    public Task Stop()                    { Outcome = FlowOutcome.Stopped;   _nextPortId = null; return Task.CompletedTask; }
    public Task Return<T>(T value)        { Outcome = FlowOutcome.Returned;  Result = value; _nextPortId = null; return Task.CompletedTask; }
    public Task Cancel()                  { Outcome = FlowOutcome.Cancelled; _nextPortId = null; return Task.CompletedTask; }

    internal int? ConsumeNext() { var n = _nextPortId; _nextPortId = null; return n; }
    internal T? ReadResult<T>() => Result is T t ? t : default;
}
```

**Default-on-no-call is `Stop`.** If an `Execute` body never calls any `flow.*` method, the flow terminates. Same as today's `default(FlowContinuation) = Stop`. Loud failures (throwing for "you forgot to set next-step") are not worth the ceremony.

##### D3 — `FlowContinuation` is removed; `Execute` returns `Task`

`FlowContinuation` was a routing token (a struct returned by `Execute` saying "go to port N" or "stop"). With `Flow` plumbed through `Execute` as a parameter, the routing decision moves onto `Flow` (via `GoTo` / `Stop` / `Return` / `Cancel`) and the return value collapses to `Task`.

- **Old:** `abstract Task<FlowContinuation> Execute(TRunner runner)`
- **New:** `abstract Task Execute(TRunner runner, Flow flow)`

`FlowContinuation.cs` is deleted. The executor's flow-walk loop becomes:

```csharp
while (current != null) {
    await current.Execute(runner, flow).ConfigureAwait(false);
    var nextPortId = flow.ConsumeNext();
    if (nextPortId == null) break;          // Stop / Return / Cancel reached
    current = TryGetFlowTarget(current.nodeId, nextPortId.Value, asset);
}
return flow;
```

**Author-facing Execute examples (the API the implementer must match for the built-in nodes):**

```csharp
// Branch
public override Task Execute(TRunner runner, Flow flow) =>
    flow.GoTo(Condition.Read() ? TruePortId : FalsePortId);

// Return<TRunner, TResult>  —  typed terminator that REPLACES the M2 untyped Return + ReturnBool.
public sealed partial class Return<TRunner, TResult> : RuntimeNode<TRunner> where TRunner : GraphRunner
{
    public InputPort<TResult> Value = null!;
    public override Task Execute(TRunner runner, Flow flow) => flow.Return(Value.Read());
}

// Cancel
public override Task Execute(TRunner runner, Flow flow) => flow.Cancel();

// Async dispatcher (CardCommandDispatcher style)
public sealed override async Task Execute(CardEffectRunner runner, Flow flow)
{
    var cmd = BuildPayload(runner);
    var result = await cmd.Execute(flow.Scope!).ConfigureAwait(false);
    WriteOutputs(result);
    await flow.GoTo(FlowOutPortId);
}
```

##### D4 — `GraphRunner.Cancelled` and `GraphRunner.ReturnValue` are removed

These were the M2 v1 boxed result channel on the runner. With D2, they don't survive — per-run state cannot live on a session-long object. Both fields go away. The information they carried lives on `Flow.Outcome` (replaces `Cancelled`) and `Flow.Result` (replaces `ReturnValue`, now typed via the controller's `TResult`).

`GraphRunner` shrinks to:

```csharp
public abstract class GraphRunner
{
    public CancellationToken CancellationToken { get; set; }   // session-default; flows can override
}
```

That's it. Subclasses (`CardEffectRunner`, etc.) add their own session-scoped services on top.

##### D5 — Entries are typed on `(TPayload, TResult)`; triggers are entries

The M2 entry shape is `EntryRuntimeNode<TEntry, TRunner>`. M3 adds `TResult`:

```csharp
public abstract class EntryRuntimeNode<TEntry, TRunner, TResult> : RuntimeNode<TRunner>
    where TEntry : class
    where TRunner : GraphRunner
{
    protected TEntry? Payload { get; private set; }
    public void SetPayload(TEntry payload) => Payload = payload;

    // Set during controller.Initialize so each entry node can be invoked directly.
    Func<TEntry, Task<TResult>>? _runFromHere;
    internal void BindRunner(Func<TEntry, Task<TResult>> runFromHere) => _runFromHere = runFromHere;

    public Task<TResult> Run(TEntry payload)
    {
        if (_runFromHere == null) throw new InvalidOperationException("Entry not initialized.");
        return _runFromHere(payload);
    }
}
```

**Marker interfaces (in attributes asm):**

```csharp
// Base — anything invocable
public interface IGraphEntry<TPayload, TResult> { }

// Sugar for the void-graph case (most card effects don't return anything)
public interface IGraphEntry<TPayload> : IGraphEntry<TPayload, Unit> { }

// Trigger subset — host treats these as auto-subscribable to events of TEvent
public interface IGraphTrigger<TEvent> : IGraphEntry<TEvent, Unit> { }

// Unit type — the v1 "no result" placeholder; lives in package
public readonly struct Unit { public static readonly Unit Default = default; }
```

**Why triggers and entries are the same node:** the difference is purely in *how* the host invokes them. An imperative entry is invoked by code calling `controller.Run<DealStrike, DamageResult>(payload)`. A trigger is invoked by an event-bus subscription that the host wires up at session start (`bus.Subscribe<DamageDealtEvent>(e => triggerNode.Run(e))`). Same node shape, same execution path, two front doors. There is **no** runtime-side concept of "listener" — that vocabulary is dead in M3.

**One TResult per graph constraint.** A graph has one entry, the entry has one TResult, every `Return` in the graph must close on the same TResult. Conflicting Returns is a validation error (EFG-V07 — see validation additions below).

##### D6 — Cross-assembly `[GraphNode]` discovery

`GraphPackageTrioEmitter.EmitGenericNodeArtifacts` currently walks only the runner's containing assembly for `[GraphNode]` types. The M2 comment `"M2 supports a single source assembly per package; consumer-authored nodes in other referenced assemblies are a v2 follow-up"` is the line we delete.

**M3 mechanism:** walk every assembly that the runner's containing assembly **references**, **filtered to assemblies that themselves reference the GraphFlow runtime asm** (i.e. the asm where `[GraphNode]` and the package's runtime base classes live). The filter is the firewall — only asms that already reference the package can possibly *define* `[GraphNode]`-attributed types, so this excludes Unity engine asms, third-party SDKs, etc., without any consumer ceremony.

**Consequences:**
- Built-in nodes (`Branch`, `Cancel`, `Not`, `Return<,>`) live in the package's runtime asm and are discovered automatically for every consumer (the consumer's runner asm references the package; the package's runtime asm references itself trivially).
- Shared node libraries (third-party `MyTeam.GraphNodes.Math.dll`) light up automatically when referenced. No explicit opt-in needed.
- Snapshot harness fixture setup mirrors production: separate package-runtime DLL reference + the per-fixture asm. The current Windows-hardcoded paths (`C:\Unity\Scaffold\Library\ScriptAssemblies\Scaffold.GraphFlow.M0.dll` etc. in `Generators/Scaffold.GraphFlow.PackageGenerator.SnapshotTests/Program.cs`) are replaced with relative / env-var-driven resolution before this lands.

##### D7 — Drop unused helper bases; shrink `[GraphPackage]`

The original M2 plan note already called for this decision pass. Settling it now:

- **Drop** `CardEntryPointNode<TEntry>` from the plan. Card entries write `EntryRuntimeNode<MyCard, CardEffectRunner, Unit>` directly. No alias, no wrapper.
- **Drop** `ListenerBase`, `ReturnBase`, `EntryBase` knobs from `[GraphPackage]`. Listeners are gone (D5). Return is a single typed built-in (`Return<TRunner, TResult>`). Entries don't need a per-package base — `EntryRuntimeNode<,,>` from the package is the base.
- **Keep** `DispatcherBase` and `CommandBase` on `[GraphPackage]`. These remain load-bearing for Mode-2 emission (the generator needs to know which open-generic to close when emitting per-payload dispatcher runtimes).

**Updated `[GraphPackage]` surface:**

```csharp
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class GraphPackageAttribute : Attribute
{
    public Type Runner { get; set; } = null!;
    public string Extension { get; set; } = "";
    public string AssetMenu { get; set; } = "";
    public PortConvention Convention { get; set; }
    public string? RegistryNamespace { get; set; }

    // Mode-2 only — wrap existing command hierarchies.
    public Type? CommandBase { get; set; }
    public Type? DispatcherBase { get; set; }
}
```

##### D8 — Listener registration is replaced by entry-catalog + host-side subscription

Today's CardSandbox `CommandPipeline` and `ICommandListener<TCmd, TResult>` collapse into the trigger model from D5. Cross-card command modification ("+1 damage on first strike") becomes a card with a trigger entry on `PreDamageDealtEvent` that mutates the event before `DealDamage` reads it.

**Concrete pattern:**
1. `DealDamageCommand.Execute` publishes `PreDamageDealtEvent { Amount = X }` on the host's event bus before computing.
2. Trigger entries on cards subscribed to `PreDamageDealtEvent` run synchronously (sequentially in the sample; host's choice in production), each mutating the event's `Amount`.
3. `DealDamageCommand` reads the (possibly modified) `Amount` and proceeds.
4. (Optionally) publishes `PostDamageDealtEvent` for reactive triggers ("when damage is dealt, draw a card").

**The framework supplies nothing here.** The event bus, subscription mechanics, ordering policy, and parallel-vs-sequential dispatch are all host territory — the framework just exposes the entry catalog (D9) and lets the host wire it however it wants.

##### D9 — Entry catalog is a list of typed entry nodes; pattern-match, no descriptors

`GraphController<TRunner>` exposes the entries it found at hydration time:

```csharp
public sealed class GraphController<TRunner> where TRunner : GraphRunner
{
    public IReadOnlyList<RuntimeNode> EntryNodes { get; }
    // Built once during Initialize by filtering asset.nodes to anything assignable to
    // EntryRuntimeNode<,,>. No descriptor class, no metadata serialised to the asset
    // beyond the existing EntryIndex list. The runtime node IS the metadata — its concrete
    // generic type carries TPayload / TRunner / TResult.

    public Task<TResult> Run<TEntry, TResult>(TEntry payload) where TEntry : class;
}
```

**Host's wiring loop (sample):**

```csharp
foreach (var card in activeCards)
foreach (var entry in card.Controller.EntryNodes)
{
    switch (entry)
    {
        case EntryRuntimeNode<DamageDealtEvent, CardEffectRunner, Unit> dmg:
            bus.Subscribe<DamageDealtEvent>(e => dmg.Run(e));
            break;
        case EntryRuntimeNode<TurnStartEvent,   CardEffectRunner, Unit> turn:
            bus.Subscribe<TurnStartEvent>(e => turn.Run(e));
            break;
        // ...
    }
}
```

**Dynamic discovery helper (optional, for hosts that want it):** one reflection helper that walks the entry's generic args and checks whether `TPayload` implements `IGraphTrigger<>`. Keep it on the side as a utility, not a core API.

#### What this means for the M2 work in flight

- `GraphRunner.Cancelled` and `GraphRunner.ReturnValue` (added M2) **are removed** in M3. Their replacement (`Flow.Outcome` / `Flow.Result`) lands as part of the same Execute-signature change.
- `ReturnBool` (added M2) **is removed** in M3 in favour of the typed `Return<TRunner, TResult>` built-in. Same machine, generalised. Existing test `M2_ReturnBool_Stores_Value` becomes a test of `Return<MySmokeRunner, bool>`.
- The M2 `[GraphNode]`-attributed `Branch<TRunner>` / `Cancel<TRunner>` / `Not` / `Return<TRunner>` move from `Assets/GraphFlowSandbox/Runtime/Nodes/` into `Assets/Packages/com.scaffold.graphflow/Runtime/Nodes/`. Their bodies update to the new Execute signature using `Flow`.
- M2's M0SmokeRuntimeTests (`M2_OnPlay_Not_Branch_Return_TruePath`, `M2_Branch_False_Cancel_Path`) update to read `flow.Outcome` / `flow.Result` instead of `runner.Cancelled` / `runner.ReturnValue`.

#### CardSandbox sample rewrite

The runtime-only CardSandbox shipped in M2-prep (`b657389`) had a `CommandPipeline` + `ICommandListener<TCmd, TResult>` model. **Both are deleted.** The sample is rebuilt against the entry-catalog + event-bus approach (D8/D9):

- `IEffectScope` stays — it's the per-run host-services bag that lives on `Flow.Scope`.
- `Command<TResult>` stays — Mode-2 commands still exist; they just execute directly against the scope without a wrapping pipeline.
- `CommandPipeline.cs` and `ICommandListener.cs` are deleted.
- `CardEffectRunner` no longer holds a `Pipeline`. It holds a scope-factory (or whatever produces `IEffectScope` per Run) and any host service refs.
- New: a tiny `EventBus` stand-in (just `Publish<T>(T)` / `Subscribe<T>(Action<T>)`) lives in the sample so the 500-Strike test has something to subscribe triggers against. **Not in the package** — the host owns its bus.
- 500 Strike sample tests are rewritten:
  - Two cards: `Strike500` (entry: `OnPlay`, plays 5 damage) and `PlusOneDamage` (entry: trigger on `PreDamageDealtEvent`, mutates `Amount += 1`).
  - Test 1: only Strike500 active → 5 damage dealt.
  - Test 2: both cards active, host wires triggers via the entry catalog → 6 damage dealt. Same outcome as today's pipeline-based test, achieved via the unified entry-and-trigger model.

#### Validation additions (`OnGraphChanged`)

M3 adds one new rule on top of M2's EFG-V01..V04:

- **EFG-V07 Conflicting Return types in a graph.** Walk all `Return<,>` nodes reachable from the entry. If their `TResult` type arguments don't all match the entry's `TResult`, error. (One TResult per graph constraint from D5.)

EFG-V05 (required-input-unwired) and EFG-V06 (data-edge type mismatch) remain deferred to M4.

#### Generator changes

- Update `GraphRegistryEmitter.EditorRegistryNamespace` to point at the package's editor namespace.
- Update `GraphPackageTrioEmitter.EmitGenericNodeArtifacts` to walk all asms that reference the package's runtime asm (D6).
- Update entry-emit path (`GraphPayloadNodeEmitter.EmitEntryRuntime` etc.) to emit `EntryRuntimeNode<TEntry, TRunner, TResult>` instead of the M2 two-arg base. The emitter reads `TResult` from the payload's `IGraphEntry<TEntry, TResult>` interface.
- Update generic-node emit (`GraphGenericNodeEmitter`) Execute body emission to match the new `(TRunner, Flow)` signature.
- Drop the per-payload `BindInput` / `GetOutputConnection` switch emit if it survives anywhere — M2 already moved most of it to the dict-based `Ports` lookup; verify no residue in Mode-2 entry emit.
- Snapshot harness: delete the hardcoded Windows DLL paths in `Program.cs`; resolve via env var (`SCAFFOLD_GRAPHFLOW_DLLS`) or a relative path under the package. Add a Cards fixture alongside the M0 fixture.

#### Sample lifecycle

- `Assets/GraphFlowSandbox/` (M0) becomes a sample. Two options:
  - **(a)** Move under `Assets/Packages/com.scaffold.graphflow/Samples~/M0Sandbox/` per UPM convention. Survives in the repo, doesn't compile by default for consumers.
  - **(b)** Delete entirely once the package's own runtime tests cover the same shapes.
  - **Default: (a)** to preserve the smoke flow + snapshot fixtures. Final call belongs to the implementer.
- `Assets/CardSandbox/` (M3-prep CardSandbox, after the rewrite above) lives under `Assets/Packages/com.scaffold.graphflow/Samples~/CardSandbox/`. Same UPM convention.

#### Implementation order (sequenced; each step keeps the build green)

1. **Package skeleton** — create `Assets/Packages/com.scaffold.graphflow/Runtime/` and `/Editor/` asmdefs; nothing inside yet but the asmdef + meta. Both compile empty.
2. **Move core runtime types** into `Runtime/`: `GraphRunner` (shrunk per D4), `RuntimeNode`, `RuntimeNode<TRunner>`, `Connection`, `Connection<T>`, `Port` and typed ports, `EntryRuntimeNode<,,>` (new third type parameter), `GraphAsset<TRunner>`, `GraphController<TRunner>`, `GraphExecutor<TRunner>`, `LifecycleInterfaces`, `Concepts`. Namespace: `Scaffold.GraphFlow`. Update asmdef GUIDs in any consumer that referenced the old M0 runtime asm.
3. **Introduce `Flow` + `FlowOutcome`** in `Runtime/Flow.cs`. Update `RuntimeNode<TRunner>.Execute` signature to `(TRunner, Flow)`. Update `GraphExecutor<TRunner>.RunFlow` to construct a `Flow`, plumb it through, and return it (or its outcome) at the end. Delete `FlowContinuation.cs`.
4. **Update built-in nodes** (`Branch`, `Cancel`, `Not`, `Return<TRunner, TResult>`) to the new Execute shape. `ReturnBool` is replaced by the typed Return; remove its file.
5. **Move editor-side core** into `Editor/`: `GraphPackageRegistry<>`, `GraphBakerCore`, `GraphAssetImporterBase`, `EditorNodeIdentity`. Namespace: `Scaffold.GraphFlow.Editor`.
6. **Update generator** (D6 cross-asm walk; new namespace; new entry emit shape; Execute-signature emit; snapshot harness path cleanup).
7. **Demote M0 sandbox** to `Samples~/M0Sandbox/`. Update its `[GraphPackage]` and references. Re-run snapshot harness against it.
8. **Rewrite CardSandbox** per the sample rewrite section above. Move to `Samples~/CardSandbox/`. Add 500-Strike + PlusOneDamage tests.
9. **Update ExecPlan-v2 status table** to mark M3 done; add M3 closeout subsection mirroring the M2 closeout style.

#### Out of scope for M3 (deferred to M4 or later)

- Required-input-unwired diagnostic (EFG-V05) — needs a `[Required]` metadata seam not designed yet.
- Data-edge type-mismatch diagnostic (EFG-V06) — preempted by GraphToolkit's UI-level wiring filter; revisit in M4 with the type-conversion mechanism.
- Listener priority / ordering — host territory by design, but we may eventually add an opt-in `[GraphTrigger(Priority = N)]` attribute. Defer.
- Multi-T `[GraphNode]` (`Equals<T>`, etc.) — already deferred from M2.
- Editor-visual API pass — already deferred from M2.
- Documentation / tutorials — bundled with M4.

#### Why this is bigger than the original M3

The original M3 was scoped as "build the CF integration helper bases and one card". That assumed the framework's runtime, runner, and entry shapes were already in their final form. They aren't:

- The runner-as-result-carrier model (M2 shipped with `GraphRunner.ReturnValue`) doesn't survive runner reuse. Fixing it is a per-run-state refactor that touches every Execute body.
- The two-arg `EntryRuntimeNode<TEntry, TRunner>` doesn't carry enough type information to make `controller.Run<TEntry, TResult>(payload)` typed at the call site. Fixing it adds a third type parameter and re-emits every entry node.
- The "core lives in M0 sandbox" reality (today, half the framework is in `Assets/GraphFlowSandbox/`) is structurally wrong for a publishable package. The relocation is mechanical but pervasive.
- Listeners-via-pipeline was the wrong abstraction once we accepted entries-as-typed; they collapse into triggers, which collapses CardSandbox's runtime.

Each of these is small individually; together they're what M3 actually is. Treating M3 as "just integrate Cards" while the framework still has these issues would either (a) ship Cards on a wobbly base, or (b) discover the same four decisions during the Cards work and grow into the same shape. We're choosing to make the structural pass first and let Cards land cleanly on top.

---

### Milestone 4 — Polish (1 week)

- Remaining built-in conventions (`MutableInReadOnlyOut`, `AllFieldsIn`).
- Remaining diagnostics: `EFG002`, `EFG003`, `EFG004`, `EFG006` (EFG005/007/008 shipped in M1).
- Per-type escape-hatch attribute handling.
- Editor menu organization (`[GraphMenu]`).
- Remaining built-in generic-node catalog deferred from M2: `Replace`, `GreaterThan`, `LessThan`, `And`, `Or`, math (`Add`/`Subtract`/`Multiply`/`Divide`/`Modulo`), conversions (`ToString`, `ToInt`, …) — added on demand when consumer graphs need them.
- **Multi-type-parameter `[GraphNode]` classes (`Equals<T>`, `IsOfType<T>`, generic `Add<T>`).** Each closed `T` needs its own editor node since GraphToolkit's picker can't pick a generic parameter at design time. Spec the registration shape: either consumer-driven (consumer authors `[GraphNodeClosure(typeof(int))]` per closed instantiation) or generator-driven (generator emits one editor mirror per `T` it finds in use across the package's payloads). Decision pass alongside the editor-visual API review.
- **`Connection` type-conversion mechanism.** Wire converters into `Connection.Bind`: registry of `(TFrom, TTo) -> Func<TFrom, TTo>`, looked up when `Connection.Bind` sees mismatched `T`s; produces an adapted `Connection<TTo>` reading through the converter. Diagnostics for ambiguous / missing converters. The architectural seam is in place from M2; M4 fills it in.
- **Editor-visual API pass (single focused review).** Today emitted editor nodes show in the GToolkit picker as their raw type names — `EchoDispatcherEditorNode`, `OnPlayEditorNode`, `BranchEditorNode`. M4 owns a focused pass that surveys what GToolkit's `Node` API actually exposes for visual customization (display name, category, icon, tooltip, port colors, search-menu path) and decides which knobs to surface as attributes vs. derive automatically. Decision points: (a) optional `Name`/`DisplayName` on `[GraphPayload]` and `[GraphNode]` vs. always-derive-from-type-name; (b) `[GraphMenu]` shape and whether it subsumes `Category` from `[GraphNode]`; (c) per-port labels — currently the field name on a typed port handle (e.g., `InputPort<bool> Condition` → port labeled "Condition") drives the editor; M4 decides whether to add an opt-in attribute for divergent display names; (d) icon/color hooks if GToolkit supports them. Output: one PR that updates attributes + emitters + snapshots together so visuals stay consistent across all emitted node kinds. Don't bolt these on piecemeal across milestones.
- Documentation: package README, conventions guide, "writing a payload" tutorial, "how to add a graph package" tutorial, "writing a generic node" tutorial.

**Total estimate:** ~7–8 weeks of focused work for v1.

---

## v2 follow-ups

Tracked but explicitly out of scope for v1.

### Asset-backed config + wizard UX (priority)

Phase 2 / Phase 3 of `[GraphPackage]` config (specced in the Source generator > Configuration section). Two stages, additive on the v1 attribute form:

- **Phase 2.** A `GraphPackageConfigAsset` ScriptableObject with a custom inspector (type pickers for runner/bases, dropdown for convention, text fields for extension/menu). On save, a custom Editor regenerates a hidden `<name>.g.cs` partial containing the equivalent `[assembly: GraphPackage(...)]`. Roslyn generator stays oblivious. `AssetPostprocessor` overwrites the `.g.cs` unconditionally on asset change; header comment marks it auto-generated.
- **Phase 3.** Project-window menu `Create > Graph Package` walks the user through runner type (or scaffolds a new one), extension, convention, base bindings, asset menu name. Outputs the asset + the runner stub + (via Phase 2's regen) the `.g.cs`. Designers and gameplay programmers never touch the attribute syntax.

Both phases are pure UX layers — no generator contract change, no breaking change for v1 consumers.

### Macros / sub-graphs (priority)

- New asset type: `EffectMacroAsset`
- Signature declaration: input/output port nodes inside the macro body
- Generated `Macro Reference` node in caller graphs (binds to a specific macro asset)
- Cycle detection across macro references
- Stale-reference handling when a macro's signature changes
- Maps to Card Framework's `CompositeAction` for runtime semantics

### Designer-managed blackboard

Full FIXED/MANAGED/VARIABLES panel with declared variables, default values, and `[Get Variable]` / `[Set Variable]` nodes. Decision between Graph Toolkit's native blackboard API and a rolled-our-own implementation is deferred until v2 — needs a spike against the real toolkit blackboard surface (not visible in current samples).

### Pluggable port-discovery strategies

Move from B2 (sealed strategy set) to B3 (consumer-provided `IPortDiscoveryStrategy`). Wait until v1's four strategies prove insufficient.

### Open generic payloads

`DealDamage<TTarget>` style. Requires per-instantiation generation or a more sophisticated runtime registry. Defer until use cases force it.

### Graph compilation to C#

If the runtime walker proves too slow on real card games (it shouldn't — there's no measurable hot path beyond the dispatch we already pay), an optional compiler emitting `Effect.Execute` C# bodies. Almost certainly never needed.

### Editor improvements

- Inline preview of variable values during play-mode debugging
- Step-through debugger for graph execution
- Visual diff between two graph versions
- Bulk operations: rename a variable across all graphs, find-all-cards-using-a-command

---

## Architectural prep for v2 (do in v1)

Specific notes to keep v2 cheap:

1. **Runtime tree walker is reentrant.** Already required for trigger listener fan-out; macros will reuse the same machinery.
2. **Registry `entryTypeId` keys are namespaced strings.** Reserve a prefix for macro references (e.g., `macro:<guid>`). Generator-emitted payloads use the fully-qualified C# type name (default — no prefix). Lets macros and generated payloads coexist in the same lookup table without collision.
3. **Asset shape is generic over node payload.** Nodes are `[SerializeReference] List<RuntimeNode<TRunner>>` of typed instances; new node categories (macro reference, sub-graph call) become new `RuntimeNode<TRunner>` subclasses, not new asset fields. Asset format stays stable across v1→v2.
4. **Controller listener API uses an interface, not a concrete listener class.** Macro-as-listener (a macro that registers triggers) becomes a different `ICommandListener<TRunner>` implementation later.
5. **Convention strategies are sealed but isolated.** Each strategy is its own class behind an internal interface; v2's pluggable extension just opens the interface.
6. **`Connection<T>` carries `SourceNode` + `SourcePortId` back-references.** Already enables traversal at hydration time; macros and dead-port elimination (v2) walk the same back-pointers.

---

## Reference: what we're explicitly NOT doing (lessons from v1)


| Anti-pattern from old `Assets/GraphFlow/`           | Why we're not repeating it                                                                                                             |
| --------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| Per-type editor `Node` classes hand-written         | Generator emits them from M1 onward; consumer writes payloads only (M0 hand-writes one of each shape as the deliberate validation slice) |
| Per-type runtime definitions hand-written           | Same                                                                                                                                   |
| Baker `switch (node)` pattern-matching              | Type-driven dispatch via registry; baker uses generated factory delegates to instantiate runtime nodes                                |
| Hardcoded blackboard wiring seeds                   | No blackboard in v1; payload-port wiring via typed `Connection<T>`, host references via consumer-authored runner-property accessor nodes |
| Reflection on field names per execution             | Typed `Connection<T>` slots wired at hydration via generated port-ID switches; hot path is a direct delegate invoke                     |
| Entry type stored as `AssemblyQualifiedName` string | Stable string `entryTypeId` from registry (`EntryIndex.entryTypeId`); bake-time validated                                              |
| Linear-flow-only with no branching                  | `Branch` node and Validate flow are first-class                                                                                        |
| Dead `IGraphTickService`                            | No tick service; consumer drives lifecycle                                                                                             |
| Vague blackboard inheritance for sub-graphs         | No sub-graphs in v1; v2 design starts from explicit semantics                                                                          |
| Flow modeled as data connections with dummy values | Separate **`FlowEdge`** list + **`FlowContinuation`** from `Execute`; data stays in `ConnectionRecord` only |


---

## Open questions for implementation phase

To resolve once we start building:

All previously-open architecture questions are resolved:


| Topic                           | Resolution                                                                                                                                                                                                                                                                                                                                                     |
| ------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Blackboard / variables          | **No blackboard in v1.** Designer constants as inline embedded port values. Host-injected references on the consumer's runner. Full blackboard deferred to v2.                                                                                                                                                                                                 |
| Runtime context                 | **`GraphRunner` abstract base class, generic `RuntimeNode<TRunner>`.** Consumer subclasses `GraphRunner` with their own services. Package has no opinion on what's on the runner.                                                                                                                                                                              |
| Editor `Graph` subclass         | **Auto-emitted by the generator** as `<R>Graph : Graph<TRunner>` (`partial class` so the consumer can extend it). Same applies to `<R>GraphAsset` and `<R>GraphImporter`. Consumer can hand-write any of the three to override; generator skips emission for the overridden one.                                                                                |
| Payload-to-package binding      | **Two modes per `[GraphPackage]`:** (1) marker interface `IGraphAction<TRunner>` / `IGraphEntry<TRunner>` for fresh code; (2) `CommandBase = typeof(...)` / `EntryBase = typeof(...)` config for wrapping existing domain hierarchies (Card Framework's `Command<>`). Generator unions both sources per runner.                                                  |
| Per-payload execution           | **`IExecutable<TRunner>` on the payload wins**, otherwise generator emits a subclass of `[GraphPackage].DispatcherBase`. Mixed within one package is allowed. Missing both → `EFG007`.                                                                                                                                                                          |
| Host-injected references        | **Consumer concern.** Package defines no contract; consumer's runner exposes whatever properties it wants and writes whatever accessor nodes it needs.                                                                                                                                                                                                         |
| Asset versioning                | **(d) Strict load + ScriptedImporter version bump.** No migration system. Bumping `[ScriptedImporter(version: N)]` triggers Unity to re-import all graphs from source. The consumer's source-graph files (whichever extension they pick) are the durable artifact; runtime assets are derived and rebakeable. Soft warning logged on `schemaVersion` mismatch. |
| Source generator implementation | **Roslyn incremental source generator** (`IIncrementalGenerator`). Modern API, better build performance, the only sensible choice on current toolchains.                                                                                                                                                                                                       |
| Graph Toolkit version           | **Pin to `0.4.0-exp.2`** (current sample version). Re-evaluate at v1 ship time if a stable release lands.                                                                                                                                                                                                                                                      |


---

## Appendix — Serialization strategy, validated against VisualNovelDirector

The single-file dual-representation design was audited against Unity's `VisualNovelDirector` sample (`Assets/Samples/Graph Toolkit/0.4.0-exp.2/VisualNovelDirector Sample/`) before finalizing this plan. Each load-bearing claim has direct evidence in the sample so we don't have to re-litigate it later:

| Claim | Evidence in sample |
| --- | --- |
| One source file holds the editor graph (as a non-main object) and the imported runtime SO (as the main object). | `VisualNovelDirectorImporter.cs`: `ctx.AddObjectToAsset("RuntimeAsset", runtimeAsset); ctx.SetMainObject(runtimeAsset);`. The `.vnd` YAML on disk has `GraphObjectImp` as one object plus the runtime SO as another. |
| The runtime asset is a real, build-shippable Unity asset (referenced by GUID from prefabs/scenes). | `BasicVisualNovelCanvas.prefab` has `RuntimeGraph: {fileID: …, guid: …}` pointing at the `.vnd`'s main object. The runtime SO is what flows through the asset DB, Addressables, and player builds. |
| The bake reads the editor graph at import time and produces a fresh typed runtime SO. The editor graph never runs. | `OnImportAsset`: `GraphDatabase.LoadGraphForImporter<VisualNovelDirectorGraph>(ctx.assetPath)` → walk → `ScriptableObject.CreateInstance<VisualNovelRuntimeGraph>()` → populate. |
| Inline port values are read at bake via `port.TryGetValue<T>()` (and `firstConnectedPort` for variable/constant nodes) and stored as typed fields on the runtime node objects — not as opaque blobs. | `GetInputPortValue<T>` in the importer; runtime nodes like `SetBackgroundRuntimeNode { public Sprite BackgroundSprite; }` carry the value as a normal serialized field, which is how `Sprite`/`UnityEngine.Object` references survive the build. |
| Game code holds only the runtime SO. | `VisualNovelDirector.cs` has `public VisualNovelRuntimeGraph RuntimeGraph;` — no reference to the editor graph type at runtime. |

**Implications for our plan:**

1. Runtime nodes are stored as typed `[SerializeReference]` instances on the asset (`GraphAsset<TRunner>` abstract base, with generator-emitted concrete subclass per `[GraphPackage]`). We do **not** store a `byte[] serializedConstants` blob — the source generator already produces a typed class per payload, so the asset can carry those instances directly and let Unity serialize their fields the normal way. This is what keeps Unity-object references (Cards, Sprites, AssetReferences, ScriptableObjects on the runner side) patch-correct through the build pipeline.
2. **VND only validates the asset-shipping story, not the connection model.** VND is a linear sequence (start node + `next` chain) baked into a flat list — it has no port-to-port wiring. Our connection model follows the FlowCanvas pattern instead: stable integer IDs (never indices) on both endpoints, `ConnectionRecord` carrying `(fromNodeId, fromPortId, toNodeId, toPortId)` as four ints, and load-time resolution that produces typed `Connection<T>` wiring objects via two virtual calls per edge — both resolving to compile-time switches the source generator wrote. See "Connection model", "Hydration", and "Wiring mechanism" sections for the explicit walkthrough.

---

*End of plan.*