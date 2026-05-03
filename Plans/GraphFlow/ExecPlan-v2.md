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
7. Zero runtime reflection on hot paths. Per-execution port reads/writes go through typed `Connection<T>` objects wired at hydration via generator-emitted `switch` jump tables — direct delegate invocation, no reflection, no string lookups.

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
│ [GraphHidden] │    │  │    (Cancel, Replace, Return*, Branch,      │
│ [GraphMenu]   │    │  │     predicates, math, conversions)         │
│ [In] / [Out]  │    │  ├─ Runtime assembly                          │
│               │    │  │  • GraphRunner (abstract)                  │
│               │    │  │  • RuntimeNode<TRunner> (abstract)         │
│               │    │  │  • Connection / Connection<T>              │
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
                  │  Per payload:                    │
                  │   • Editor Node subclasses       │
                  │   • Runtime node + Ports consts  │
                  │   • BindInput / GetOutputConn    │
                  │     switch overrides             │
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

- **`ConnectionRecord`** — **data only**. Hydration builds typed `Connection<T>` handles; `BindInput` / `GetOutputConnection` never carry flow.
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

**Hand-written, shipped in the package** (finite set):

- `[Cancel]` — generic early-exit. One reason-string input.
- `[Replace]` — generic early-exit. One typed command reference input.
- `[Return]` — void terminator. No inputs.
- `[Return Bool]` — bool terminator. One bool input.
- `[Branch]` — flow control. Bool input, two flow outputs (true / false).
- Predicates: `[Equals]`, `[NotEquals]`, `[GreaterThan]`, `[LessThan]`, `[AND]`, `[OR]`, `[NOT]`, `[IsOfType]`.
- Math: `[Add]`, `[Subtract]`, `[Multiply]`, `[Divide]`, `[Modulo]`. Numeric typing per Graph Toolkit conventions.
- Conversions: `[ToString]`, `[ToInt]`, etc. — minimal set as needed.

**Host-context accessors** — small set of consumer-defined `RuntimeNode<TRunner>` subclasses that read typed properties off the consumer's runner (e.g., `[Get Owner]` returns `runner.Owner`). The package defines no contract for these; the consumer writes whatever accessor nodes their game needs.

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
  - A nested `static class Ports` of `const int` IDs (one per port — stable hash of the field name; `[GraphPort(Id = N)]` overrides for rename-stability)
  - Inline-value fields for designer constants (serialized into the asset by Unity)
  - `[NonSerialized] Connection<T>` slots for inputs and `[NonSerialized] T` slots for outputs (runtime-only)
  - Generated `BindInput(int portId, Connection)` and `GetOutputConnection(int portId)` overrides — `switch` jump tables keyed on the constants in `Ports`
- **`Execute` returns `ValueTask<FlowContinuation>`** — either stop the flow walk or name the **flow output port id** to follow next (Branch returns true/false port ids; linear nodes return their single flow-out id).
- One registry partial-class entry mapping the payload type id to the runtime node class plus an editor-port-name → port-ID lookup the baker uses when translating editor wires

For each entry-shaped payload (an `IGraphEntry<TRunner>` implementer or a descendant of the package's `EntryBase`): same shape, just 1 editor + 1 runtime node instead of 3.

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
2. **The `Ports` constants are the single source of truth.** Both the runtime node's `BindInput`/`GetOutputConnection` switches and the registry's editor-port-name → port-ID lookup the baker reads come from the same generated constants. There's no second list to keep in sync.
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

Ship the structural ones (EFG005, EFG007, EFG008); add the rest as friction emerges.

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
2. **Node translation.** Each editor node maps to one or more typed runtime node instances. The baker reads the registry to find the **generator-emitted factory delegate** (`Func<RuntimeNode<TRunner>>`) for a given editor node type, invokes it to construct the runtime node, populates its inline-value fields from the editor node's port constants (via `port.TryGetValue<T>()` for unwired embedded values, or `port.firstConnectedPort` chasing for variable/constant nodes — same approach as VND's `GetInputPortValue`), and assigns the stable `nodeId` from step 1. No `Activator.CreateInstance`, no reflection — the factory delegate is one of the pieces the source generator emits per payload. Most editor nodes map 1:1 (`StrikeDispatcherNode` → `StrikeDispatcherRuntime`); some may be 1:N if a built-in node compiles to multiple runtime steps.
3. **Connection resolution (data only).** Editor **data** port-to-port connections are translated into `ConnectionRecord`s of `(fromNodeId, fromPortId, toNodeId, toPortId)` — all four ints. **Flow** connections are translated into `FlowEdge`s of `(fromNodeId, fromFlowPortId, toNodeId, toFlowPortId)` — execution ordering only; no `Connection<T>`.
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

    // Generated overrides on every concrete runtime node — see "Wiring mechanism" below
    public abstract Connection GetOutputConnection(int portId);
    public abstract void       BindInput(int portId, Connection connection);
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
- **Stable `portId` (int).** Assigned by the source generator, derived deterministically from the C# field name (e.g. FNV-1a hash) so that source-order changes don't move IDs. An explicit `[GraphPort(Id = N)]` escape hatch lets a consumer freeze an ID across a rename. Adding a new field reserves a fresh ID; removing one retires it. The generator emits the IDs as `const int` constants on a per-payload static class, both for the runtime node's `BindInput`/`GetOutputConnection` switch labels and for the baker to read when translating editor port names to runtime port IDs.
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
   var conn = from.GetOutputConnection(c.fromPortId);  // virtual call → generated switch on portId
   to.BindInput(c.toPortId, conn);                     // virtual call → generated switch on portId
   ```
   Two virtual calls per **data** edge. Flow never uses `BindInput`.

3. **Bind entry payloads at dispatch.** When `Run<TEntry>(payload)` runs, the controller calls `SetPayload` on the root when it inherits `EntryRuntimeNode<TEntry, TRunner>`.

4. **Flow execution (separate from hydration).** The executor starts at the entry root, runs `Execute`, reads `FlowContinuation`, and follows `flowEdges` by matching `(fromNodeId, fromFlowPortId)` — then repeats on the target node. Pure data nodes reached only via flow may run when flow enters them (linear graphs); Branch (M2) selects among multiple outgoing flow ports via `FlowContinuation`.

5. **Build the entry index.** `var entryByType = new Dictionary<Type, RuntimeNode<TRunner>>();` Walk `asset.entries`, resolve `entryTypeId` to `Type` (registry / `AssemblyQualifiedName` in M0), look up the root node in `byId` by `rootNodeId`, populate the dictionary.

6. **Lifecycle pass.** Walk `asset.nodes` once and:
   - For any node implementing `IInitializableNode<TRunner>`, call `Initialize(runner)`. Nodes can cache typed services off the runner here.
   - For any node implementing `IListenerNode<TRunner>`, collect it into `CommandListeners` for the consumer to register with their pipeline.

7. **Done.** The controller holds hydrated data wiring (`Connection<T>` on inputs), entry lookup, and listener hooks. The executor walks flow separately using `flowEdges` + `FlowContinuation`.

#### Wiring mechanism — no reflection

The "lookup" in step 2 is **two virtual calls**, both resolving to switches the source generator wrote at compile time.

For each generated runtime node, the generator emits the wiring scaffolding (port IDs + slots + the two switch overrides) identically regardless of execution path. The `Execute` body is the only piece that varies per the decision tree (`IExecutable` vs `DispatcherBase` — see "Per-payload emission").

Wiring scaffolding (every generated runtime node has exactly this shape):

```csharp
// Generated, per payload — example: StrikeDispatcherRuntime (Mode 2 / DispatcherBase path)
public sealed class StrikeDispatcherRuntime : CardCommandDispatcher<Strike, StrikeResult> {
    // Stable port-ID constants (FNV-1a of field name, or [GraphPort(Id=N)] override).
    // Same constants are used by the baker when translating editor port names to runtime IDs.
    public static class Ports {
        public const int Magnitude   = 0x9F3A_C12B;   // input
        public const int Target      = 0x4D81_77E0;   // input
        public const int DamageDealt = 0x2C5B_AA09;   // output
    }

    // Inline designer constants — serialized into the asset by Unity
    public int  Magnitude;
    public Card Target;

    // Runtime-only — populated at hydration / flow time, NOT serialized
    [NonSerialized] public Connection<int>  _in_Magnitude;
    [NonSerialized] public Connection<Card> _in_Target;
    [NonSerialized] public int              _out_DamageDealt;

    // Generated: produce a typed Connection<T> for an output port
    public override Connection GetOutputConnection(int portId) => portId switch {
        Ports.DamageDealt => new Connection<int>(this, Ports.DamageDealt, () => _out_DamageDealt),
        _ => throw new ArgumentOutOfRangeException(nameof(portId)),
    };

    // Generated: store a typed Connection<T> in the right input slot
    public override void BindInput(int portId, Connection connection) {
        switch (portId) {
            case Ports.Magnitude: _in_Magnitude = (Connection<int>)connection;  return;
            case Ports.Target:    _in_Target    = (Connection<Card>)connection; return;
            default: throw new ArgumentOutOfRangeException(nameof(portId));
        }
    }

    // ── Execution path varies by [GraphPackage] decision tree ──

    // Mode 2: inherits Execute from CardCommandDispatcher<Strike, StrikeResult>;
    // generator only fills in BuildPayload / WriteOutputs:

    protected override Strike BuildPayload() => new Strike {
        Magnitude = _in_Magnitude != null ? _in_Magnitude.Read() : this.Magnitude,
        Target    = _in_Target    != null ? _in_Target.Read()    : this.Target,
    };

    protected override void WriteOutputs(StrikeResult result) {
        _out_DamageDealt = result.DamageDealt;
    }

    // Mode 1 (IExecutable on payload) alternative would replace the two methods above with:
    //   public override ValueTask Execute(CardEffectRunner runner) {
    //       var payload = BuildPayload();
    //       return payload.Execute(runner);   // delegates to IExecutable<TRunner>
    //   }
    // and the class would inherit RuntimeNode<CardEffectRunner> directly, not CardCommandDispatcher.
}
```

Why this works without reflection:

- **`switch` on `int`** compiles to a jump table — O(1), no `Dictionary` lookup at hydration.
- **`(Connection<T>)connection`** is a reference cast on a sealed generic class. For value-typed ports (`int`, `float`, struct), the typed value lives inside the closure captured by the `Connection<int>`'s `Func<T>`; the cast does not box the underlying value, and `Connection<T>.Read()` JIT-inlines to a direct delegate invoke that returns the typed `T`.
- **Closure allocation** (`() => _out_DamageDealt`) is one heap allocation per output port per node, paid once at hydration. The hot path is `_in_Magnitude.Read()` — direct typed call.
- **Stable port IDs in the switch labels** mean a generator change that adds or removes a port in the source order doesn't shift any other port's ID. Existing assets keep wiring correctly. Removed ports become bake-time errors with a clear "port `Magnitude` no longer exists on `Strike`" diagnostic.

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

    public sealed class OnPlayRuntime : RuntimeNode<MyRunner>
    {
        public static class Ports
        {
            public const int CardId = 0x4F2A_8B17;   // FNV-1a("CardId")
        }

        [NonSerialized] public int _out_CardId;

        public override Connection GetOutputConnection(int portId) => portId switch
        {
            Ports.CardId => new Connection<int>(this, Ports.CardId, () => _out_CardId),
            _            => throw new ArgumentOutOfRangeException(nameof(portId)),
        };

        public override void BindInput(int portId, Connection connection) =>
            throw new ArgumentOutOfRangeException(nameof(portId));   // no inputs

        public override ValueTask Execute(MyRunner runner) { /* write _out_CardId, walk flow */ }
    }
}
```

```csharp
// AUTO-GENERATED — from Log : IGraphAction<MyRunner>, IExecutable<MyRunner>
// Decision: payload implements IExecutable<MyRunner> → emit self-executing runtime node.
namespace MyGame.Graph.Generated
{
    public sealed class LogDispatcherEditorNode : Node { /* input port: Message */ }

    public sealed class LogDispatcherRuntime : RuntimeNode<MyRunner>
    {
        public static class Ports
        {
            public const int Message = 0x77E1_3C20;   // FNV-1a("Message")
        }

        public string Message;                                  // inline default (serialized)
        [NonSerialized] public Connection<string> _in_Message;  // wired at hydration

        public override Connection GetOutputConnection(int portId) =>
            throw new ArgumentOutOfRangeException(nameof(portId));

        public override void BindInput(int portId, Connection connection)
        {
            switch (portId)
            {
                case Ports.Message: _in_Message = (Connection<string>)connection; return;
                default: throw new ArgumentOutOfRangeException(nameof(portId));
            }
        }

        public override ValueTask Execute(MyRunner runner)
        {
            var payload = new Log
            {
                Message = _in_Message != null ? _in_Message.Read() : this.Message,
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
5. **Hydrate.** Game runs, `Awake()` builds `MyRunner`, constructs `GraphController<MyRunner>(graphAsset)`, calls `Initialize(runner)`. Hydration indexes nodes by id, walks each `ConnectionRecord` calling `from.GetOutputConnection(fromPortId)` then `to.BindInput(toPortId, conn)` — every `Connection<T>` slot is now populated.
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
| M2+ | Planned | Editor nodes, flow semantics, CF integration per sections below. |

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
- Per-payload runtime emission: `static class Ports` constants, `[NonSerialized] Connection<T>` slots, `BindInput` / `GetOutputConnection` switches.
- Per-payload execution path: `IExecutable<TRunner>` detection → self-executing `Execute`; otherwise `DispatcherBase` close + `BuildPayload`/`WriteOutputs` emission.
- Registry partial-class emission (editor type → runtime type + port-name → port-ID).
- Conventions: `CommandResultPair` and `AttributedFields` (the two that exercise the strategy abstraction).
- Diagnostics: `EFG005` (missing result pair), `EFG007` (no execution path), `EFG008` (multi-package binding).
- Snapshot tests: take the M0 hand-written file, run the generator on the corresponding payload, diff against the hand-written file. Generator is correct iff diff is empty.

### Milestone 2 — Editor authoring (1.5 weeks)

- Hand-written generic nodes (Cancel, Replace, Return, Return Bool, Branch, predicates, math, conversions) — generic over `TRunner`.
- Validate / Run flow port handling on generated entry / listener nodes.
- `[Return Strike]` (and friends) typed terminator with unwired-default semantics.
- `OnGraphChanged` edit-time validation rules.
- (No blackboard panel — deferred to v2.)

### Milestone 3 — Card Framework integration (1 week)

- `CardEffectRunner : GraphRunner` with `IEffectScope`, host references, services.
- `[assembly: GraphPackage(Runner = typeof(CardEffectRunner), CommandBase = typeof(Command<>), DispatcherBase = typeof(CardCommandDispatcher<,>), ...)]` — Mode 2 binding, payloads untouched.
- Helper bases: `CardCommandDispatcher<TCmd, TResult>`, `CardCommandListener<TCmd, TResult>`, `ReturnPayloadNode<TCmd>`, `CardEntryPointNode<TEntry>` — all `RuntimeNode<CardEffectRunner>`.
- `ICommandListener<CardEffectRunner>` adapter to the framework's pipeline registration interface.
- One real card authored end-to-end (recreate `500 Strike` from Overknights).
- Documented integration recipe.
- Decision pass on whether `EntryNodeBase` / `ListenerBase` / `ReturnBase` actually pulled weight; drop the unused ones from `[GraphPackage]` if so.

### Milestone 4 — Polish (1 week)

- Remaining built-in conventions (`MutableInReadOnlyOut`, `AllFieldsIn`).
- Remaining diagnostics: `EFG002`, `EFG003`, `EFG004`, `EFG006` (EFG005/007/008 shipped in M1).
- Per-type escape-hatch attribute handling.
- Editor menu organization (`[GraphMenu]`).
- Documentation: package README, conventions guide, "writing a payload" tutorial, "how to add a graph package" tutorial.

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