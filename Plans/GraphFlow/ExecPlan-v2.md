# ExecPlan v2 — Effect Graph Tooling

## Context

The previous attempt (`ExecPlan.md`, `Assets/GraphFlow/`) over-engineered a generic flow-graph engine that depended on a source generator that never landed. The result: per-type editor classes, per-type runtime definitions, per-type asset baker cases, hardcoded wiring seeds, half-implemented branching, dead-code services. Working tests, painful architecture.

This plan defines a focused, source-generator-driven graph tool for authoring **game effects** — the visual scripting of card abilities. Built on Unity Graph Toolkit, designed to integrate with the Card Framework's command pipeline, but intended to be game-agnostic.

The reference for the **designer-facing surface** is the existing FlowCanvas-based EffectFlow system in the Overknights project. The reference for the **runtime semantics** is the Card Framework planning docs (`C:\Unity\Card Framework\Product\`).

---

## Goals

1. Designers author card effects visually with zero code per effect.
2. Adding a new command or entry-point type to the game = write a payload class. Generator emits all editor/runtime/asset/registry plumbing.
3. Multiple entry points per card graph (`Play`, `Execute`, `Dispose`, `On Before Strike`, `On After Heal`, etc.).
4. `Validate` + `Run` dual flows on every entry node, native to the graph language.
5. Trigger listeners can pass-through-with-modifications, cancel, or replace the in-flight command.
6. Package is game-agnostic — works for Card Framework, FlowCanvas-shaped systems, or any custom convention via configuration.
7. Zero runtime reflection on hot paths. All per-execution port reads/writes go through generated delegates.

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
┌────────────────────────────────────────────────────────────────────┐
│  CONSUMER (e.g. Overknights game)                                  │
│  • Defines payload types (GameCommand subclasses, EntryPoint subs)  │
│  • Declares [assembly: GraphConfig(...)]                     │
│  • Game code drives controller.Invoke<T>() and listener registration│
└──────────────────┬─────────────────────────────────────────────────┘
                   │
        ┌──────────┴──────────┐
        ▼                     ▼
┌───────────────┐    ┌─────────────────────────────────────────────┐
│ ATTRIBUTES    │    │  PACKAGE                                     │
│ (zero-dep DLL)│    │  ├─ Editor assembly                          │
│               │    │  │  • Graph subclass (asset)                 │
│ [GraphHidden] │    │  │  • ScriptedImporter (bake)                │
│ [GraphPort]   │    │  │  • Hand-written generic nodes             │
│ [GraphMenu]   │    │  │    (Cancel, Replace, Return*, Branch,     │
│ [In] / [Out]  │    │  │     predicates, math, conversions)        │
│               │    │  ├─ Runtime assembly                         │
│               │    │  │  • GraphRunner (abstract base)            │
│               │    │  │  • RuntimeNode<TRunner> base              │
│               │    │  │  • GraphController<TRunner>         │
│               │    │  │  • GraphExecutor<TRunner> (tree walker)   │
│               │    │  │  • IInitializableNode<TRunner> / IListenerNode<TRunner> │
│               │    │  │  • GraphAsset<TRunner> ScriptableObject   │
│               │    │  └─ Generator assembly (Roslyn)              │
│               │    │     • [assembly: GraphConfig] reader   │
│               │    │     • Convention strategies (4 built-in)     │
│               │    │     • Per-type emission                      │
└───────────────┘    └─────────────────────────────────────────────┘
                                │
                                ▼
                  ┌─────────────────────────────┐
                  │  GENERATED ASSEMBLY          │
                  │  (one per consumer assembly) │
                  │                              │
                  │  • Per-payload editor nodes  │
                  │  • Per-payload runtime nodes │
                  │  • Port-ID constants + switch│
                  │  • Registry partial extension│
                  └─────────────────────────────┘
```

Three layers, three assemblies in the package, one generated assembly per consumer.

---

## Concept layer

### Two payload hierarchies

`**EntryPoint**` — object-specific, direct invocation. Only the graph that owns the entry node receives it.

```csharp
public abstract class EntryPoint { }

public class Play    : EntryPoint { public bool special; }
public class Execute : EntryPoint { }
public class Dispose : EntryPoint { }
public class Attach  : EntryPoint { public Card target; }
```

`**GameCommand<TResult>**` — pipelined through the framework. Any graph can listen via `On Before X` / `On After X`.

```csharp
public abstract class GameCommand<TResult> where TResult : CommandResult { }
public abstract class CommandResult { }

public sealed record StrikeResult : CommandResult {
    public int DamageToCore;
    public int DamageToShield;
}

public sealed record Strike : GameCommand<StrikeResult> {
    public int Magnitude;
    public Player Owner;
    public bool Unblockable;
}
```

The **base classes are consumer-defined**. The graph package never imports them. The generator finds them via syntax-tree analysis of the consumer's configured base names.

### Flows are typed functions

Every flow output port on an entry/listener node is a function with a known signature:


| Flow                                 | Inputs                  | Return                                       |
| ------------------------------------ | ----------------------- | -------------------------------------------- |
| Entry node — Validate                | EntryPoint fields       | `bool`                                       |
| Entry node — Run                     | EntryPoint fields       | `void`                                       |
| Trigger listener (Before) — Validate | Command fields          | `bool`                                       |
| Trigger listener (Before) — Run      | Command fields          | the Command (modified) — or Cancel / Replace |
| Trigger listener (After) — Validate  | Command + Result fields | `bool`                                       |
| Trigger listener (After) — Run       | Command + Result fields | `void`                                       |


Only **Before-trigger Run flows** have a non-bool, non-void return — and that return type is always the listened-to Command. This is the only place the generator emits a typed `Return T` node.

### Modification semantics for Before triggers

Three legal outcomes from a Before-trigger Run flow:

- **Pass-through (with optional field modifications)** — terminate via `[Return Strike]` (typed, generated). Each input port defaults to "use original value" if unwired (Graph Toolkit embedded port values).
- **Cancel** — terminate via generic `[Cancel]` node (takes a reason string). Aborts the in-flight command.
- **Replace** — terminate via generic `[Replace]` node (takes any `GameCommand` reference). Substitutes a different command into the pipeline.

Flow-end without a terminator on a non-void Run flow is a **graph-validation error** at edit time.

### Variables (deferred to v2)

v1 ships **no** designer-managed blackboard. Two paths cover the use cases:

- **Host-injected references** (Owner, Card, Parent, …) — typed properties on the consumer's runner subclass, read by consumer-authored accessor nodes.
- **Designer constants** (Magnitude = 7, EnergyCost = 3) — inline embedded port values via Graph Toolkit's `port.TryGetValue<T>` mechanism.

A full FIXED/MANAGED/VARIABLES blackboard with declared shared variables is a v2 follow-up. See "Host-injected references" below for the v1 substitute.

---

## Authoring surface

### Node taxonomy

**Generated per `EntryPoint` subclass** (1 editor node):

- `OnPlayNode`, `OnExecuteNode`, `OnDisposeNode`, … — direct-invoke entry nodes with `Validate` + `Run` flow output ports plus data output ports for the entry's fields.

**Generated per `GameCommand<TResult>` subclass** (3 editor nodes):

- `StrikeDispatcherNode` — used in graph bodies; ports = command's input fields + result's output fields. Executes `pipeline.Dispatch(payload)`.
- `OnStrikeListenerNode` — entry-style with Before/After enum dropdown. Validate + Run flow ports. Output data ports for command fields (Before) or command + result fields (After). Registers with the consumer's pipeline through the controller's listener API.
- `ReturnStrikeNode` — typed terminator for Before-trigger Run flows. Input ports for command fields with "use original" defaults.

**Hand-written, shipped in the package** (finite set):

- `[Cancel]` — generic early-exit. One reason-string input.
- `[Replace]` — generic early-exit. One `GameCommand` reference input.
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

The consumer declares a single assembly-level attribute:

```csharp
[assembly: GraphConfig(
    EntryBases        = new[] { "MyGame.EntryPoint" },
    CommandBases      = new[] { "MyGame.GameCommand`1" },
    Convention        = PortConvention.CommandResultPair,
    RegistryNamespace = "MyGame.Effects.Generated",

    // Optional: consumer-supplied helper bases the generator extends.
    // If unset, generated runtime nodes inherit directly from RuntimeNode<TRunner>
    // (and the consumer is responsible for providing Execute via partial class).
    EntryNodeBase  = "MyGame.EntryNodeBase`1",            // generic over <TPayload>
    DispatcherBase = "MyGame.CommandDispatcherNode`2",    // generic over <TCmd, TResult>
    ListenerBase   = "MyGame.CommandListenerNode`2",      // generic over <TCmd, TResult>
    ReturnBase     = "MyGame.ReturnPayloadNode`1"         // generic over <TCmd>
)]
```

- **Required**. Compile error `EFG001` if missing.
- `CommandBases` and `EntryBases` are lists — supports multiple base types per category (e.g., separating `GameAction` from `GameSignal`).
- Base type names are fully-qualified strings; generator does syntax-only matching, no compilation-side type loading.
- The four `*Base` fields tell the generator what hand-written abstract class each emitted runtime node should inherit from. The arity-suffixed name (`` `1 ``, `` `2 ``) is the open generic the generator closes over the payload (and result) type. Concrete-emitted classes look like `class StrikeDispatcherRuntime : CommandDispatcherNode<Strike, StrikeResult>`. The base classes are where the consumer puts the actual `Execute(runner)` body that does the domain work; the generator's emitted subclass only plugs in the typed payload-construction and port-binding plumbing.

### Built-in conventions


| Convention             | Inputs                   | Outputs                                                     |
| ---------------------- | ------------------------ | ----------------------------------------------------------- |
| `CommandResultPair`    | command's public members | paired result's public members (via `GameCommand<TResult>`) |
| `AttributedFields`     | `[In]`-tagged            | `[Out]`-tagged                                              |
| `MutableInReadOnlyOut` | settable members         | init/get-only members                                       |
| `AllFieldsIn`          | all public members       | (none)                                                      |


`AttributedFields` and the per-type escape hatches (`[GraphHidden]`, `[GraphPort]`, `[GraphMenu]`) live in a tiny attributes-only DLL the package ships. Consumers reference it only if they use it.

### Per-payload emission

For a `GameCommand<TResult>` subclass with N input fields and M output fields:

- 3 editor `Node` subclasses (Dispatcher, Listener, Return)
- 3 runtime node classes (matching counterparts), each with:
  - A nested `static class Ports` of `const int` IDs (one per port — stable hash of the field name; `[GraphPort(Id = N)]` overrides for rename-stability)
  - Inline-value fields for designer constants (serialized into the asset by Unity)
  - `[NonSerialized] Connection<T>` slots for inputs and `[NonSerialized] T` slots for outputs (runtime-only)
  - Generated `BindInput(int portId, Connection)` and `GetOutputConnection(int portId)` overrides — `switch` jump tables keyed on the constants in `Ports`
- One registry partial-class entry mapping the payload type id to the runtime node class plus an editor-port-name → port-ID lookup the baker uses when translating editor wires

For an `EntryPoint` subclass with N fields: same shape, just 1 editor + 1 runtime node instead of 3.

The two key invariants the generator must hold:

1. **Port IDs are stable across source-order changes.** Adding a new field reserves a fresh ID; removing one retires it; an existing field keeps its ID even if the field above it is deleted. Default derivation is FNV-1a of the field name. `[GraphPort(Id = N)]` lets the consumer freeze an ID across a rename.
2. **The `Ports` constants are the single source of truth.** Both the runtime node's `BindInput`/`GetOutputConnection` switches and the registry's editor-port-name → port-ID lookup the baker reads come from the same generated constants. There's no second list to keep in sync.

### Resolved generator behavior


| Decision                    | Resolution                                          |
| --------------------------- | --------------------------------------------------- |
| Config required             | Yes; missing → `EFG001`                             |
| Conventions per assembly    | One only (split assemblies if mixed needed)         |
| Abstract bases              | Skipped automatically, only instantiable types emit |
| Open generics               | Not supported in v1; emits warning if encountered   |
| Registry shape              | `static partial class` so consumers can extend      |
| Nullable types              | Pass through as-is                                  |
| Field ordering              | Source order for editor display; **does NOT determine port ID** (port IDs are stable hashes) |
| Port ID stability           | Stable int per field (default: FNV-1a of name); `[GraphPort(Id = N)]` to freeze across renames |
| Multiple bases per category | Supported (list in config)                          |


### Compile-time diagnostics (nice-to-have)

- `EFG002` — `[In]` on a `readonly` field
- `EFG003` — `[Out]` on a settable field (in `AttributedFields` mode)
- `EFG004` — Field type not serializable
- `EFG005` — `GameCommand` subclass without paired result type (in `CommandResultPair` mode)
- `EFG006` — Field name conflicts with a generated port label

Ship the structural ones (EFG001, EFG005); add the rest as friction emerges.

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

Following Unity's `VisualNovelDirector` sample: **one source file on disk** (with an extension the consumer picks — e.g. `.cardgraph` for the Card Framework integration) holds both the editor graph (hidden, for re-editing) and the imported runtime asset (the `SetMainObject`, what consumers reference). Designers drag the file into a `GraphAsset<TRunner>` field (or a consumer-defined concrete subclass like `CardEffectGraphAsset`) and get the runtime form directly.

**Custom Graph subclass** — registers the file extension and is the Graph Toolkit edit-time asset type. Written by the consumer (illustrated here as `CardEffectGraph` for the Card Framework integration; non-card consumers write their own analogue):

```csharp
[Graph("cardgraph")]
public sealed class CardEffectGraph : Graph<CardEffectRunner> {
    [MenuItem("Assets/Create/Effects/Card Effect")]
    static void CreateNew() =>
        GraphDatabase.PromptInProjectBrowserToCreateNewAsset<CardEffectGraph>("New Effect");

    public override void OnGraphChanged(GraphLogger logger) {
        // Light edit-time validation — flags errors as designers wire (terminator presence,
        // type mismatches, unwired required ports). This drives the inline error UI;
        // it does NOT determine whether the bake succeeds.
    }
}
```

`[Graph("cardgraph")]` registers the extension with Unity. `PromptInProjectBrowserToCreateNewAsset` is the standard creation API — no `[CreateAssetMenu]`, no custom `CreateInstance` plumbing. The package supplies the `Graph<TRunner>` base; consumers pick their extension and runner type.

**What Graph Toolkit serializes for us** (auto, into the `.cardgraph` YAML):

- Editor node identities, types, positions
- Port-to-port connections (between editor nodes only)
- Embedded port values (constants on unwired ports)
- Per-node `[Serializable]` fields

**What Graph Toolkit does NOT serialize:**

- Anything runtime-shaped: no resolved port-binding delegates, no payload instances, no execution order, no flow successor pointers, no validated tree structure.

v1 does not use Graph Toolkit's blackboard surface; designer constants live as embedded port values, host-context references live on the consumer's runner. The editor graph is just a designer's authoring artifact. It can be in any state — including states that don't bake. Our runtime model is separate.

### The bake step (editor → runtime)

The ScriptedImporter is where editor connections become runtime connections. It is a real translation, not a copy. The package provides a reusable base class; the consumer writes a thin subclass with the version and extension attribute:

```csharp
// Package
public abstract class GraphAssetImporterBase<TGraph, TRunner> : ScriptedImporter
    where TGraph : Graph<TRunner> where TRunner : GraphRunner
{
    public override void OnImportAsset(AssetImportContext ctx) {
        var editorGraph = GraphDatabase.LoadGraphForImporter<TGraph>(ctx.assetPath);
        if (editorGraph == null) {
            ctx.LogImportError("Failed to load graph; asset is corrupt or version-mismatched.");
            return;
        }

        var bakeResult = GraphBaker.Bake<TGraph, TRunner>(editorGraph);
        foreach (var diag in bakeResult.Diagnostics)
            ctx.LogImportError(diag.Message, ctx.assetPath);

        if (bakeResult.HasErrors) return;

        ctx.AddObjectToAsset("Runtime", bakeResult.Asset);
        ctx.SetMainObject(bakeResult.Asset);
    }
}

// Consumer
[ScriptedImporter(version: 1, ext: "cardgraph")]
internal sealed class CardEffectGraphImporter
    : GraphAssetImporterBase<CardEffectGraph, CardEffectRunner> { }
```

`GraphBaker` is responsible for **everything that turns an editor graph into a runtime-compliant artifact**:

1. **Node-id assignment (stable across re-bakes).** The baker reads the previous runtime asset at `ctx.assetPath` (if any) and walks its `nodes`, recovering an `editorGuid → nodeId` map from each runtime node's `editorGuid` field. Every editor node Graph Toolkit reports gets translated to a stable `int nodeId`: an existing editor guid keeps its previous id; a new editor node gets the next free monotonic int (highest seen + 1). Removed editor nodes' ids are retired. Each emitted runtime node carries both its new `nodeId` and the source `editorGuid` so the next re-import can recover the map. Runtime hydration ignores `editorGuid`.
2. **Node translation.** Each editor node maps to one or more typed runtime node instances. The baker reads the registry to find the runtime class for a given editor node type, then `Activator.CreateInstance` (or a generated factory in the registry) constructs the runtime node, populates its inline-value fields from the editor node's port constants (`port.TryGetValue<T>`), and assigns the stable `nodeId` from step 1. Most editor nodes map 1:1 (`StrikeDispatcherNode` → `StrikeDispatcherRuntime`); some may be 1:N if a built-in node compiles to multiple runtime steps.
3. **Connection resolution.** Editor port-to-port connections are translated into `ConnectionRecord`s of `(fromNodeId, fromPortId, toNodeId, toPortId)` — all four ints. The baker resolves each editor-port-name to its stable `portId` by looking up the registry's per-payload `editor-port-name → port-ID` table (emitted by the generator from the same `Ports` constants the runtime switches use). Connections involving editor-only helper nodes are inlined; the resulting `ConnectionRecord` binds directly to the upstream non-helper source.
4. **Embedded value extraction.** Each unwired input port's constant value is read via `port.TryGetValue<T>()` and stored into the matching typed field on the runtime node instance.
5. **Bake-time validation.** Errors that prevent runtime correctness are reported here, even if the editor accepted them:
  - Required ports unwired and lacking a constant
  - Type mismatches Graph Toolkit didn't catch
  - **A connection's `fromPortId` or `toPortId` no longer exists on its node's runtime type** (catches generator-side breakage when a payload field is removed)
  - Missing terminators on non-void Run flows (`[Return X]` / `[Cancel]` / `[Replace]`)
  - Cycles in flow paths
  - Entry node referring to an `EntryPoint` type that no longer exists in the registry
  - Listener node referring to a `GameCommand` type that's been removed
6. **Entry-point indexing.** Walks all entry/listener nodes, builds the `entries` list mapping bound `entryTypeId` (string registry key) to the runtime root `nodeId` (int).
7. **Schema version stamping.** The runtime asset records the current schema version (paired with the importer version — see asset versioning below).

A graph that passes `OnGraphChanged` validation can still **fail to bake**. That's fine and expected — `OnGraphChanged` runs on every keystroke and does cheap structural checks; the baker does deeper analysis (whole-graph cycles, registry lookups, type cross-validation). On bake failure, no runtime asset is emitted; the source file becomes "edits but doesn't build" and the Inspector / Console explains why.

### Runtime asset format

The runtime asset is a real Unity `ScriptableObject` — added as a sub-asset and made the main object via `ctx.SetMainObject` (same pattern as the VisualNovelDirector sample). That means: it survives builds, ships through Addressables, can be referenced from prefabs and scenes by GUID, shows up in the Inspector. **It is an asset, not a serializable POCO.** The editor graph stays inside the same source file as a non-main object; nothing at runtime touches it.

Serialization is **plain Unity ScriptableObject serialization** — we ride Unity's machinery, we do not layer a custom one on top.

The package ships an **abstract, runner-typed base class**. The package is game-agnostic; it never names "effect" or anything domain-specific. Consumers (Card Framework, Overknights, anyone) subclass it concretely:

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
    public abstract ValueTask Execute(TRunner runner);
}

// All four fields are IDs — never list indices. Stable across re-bakes.
[Serializable]
public struct ConnectionRecord {
    public int fromNodeId;
    public int fromPortId;
    public int toNodeId;
    public int toPortId;
}

[Serializable]
public struct EntryIndex {
    public string entryTypeId;         // payload type id from the registry (string — registry-stable)
    public int    rootNodeId;          // stable integer id of the runtime root for that entry
}

public abstract class GraphAsset<TRunner> : ScriptableObject where TRunner : GraphRunner {
    [SerializeReference] public List<RuntimeNode<TRunner>> nodes;          // typed, polymorphic
    public                List<ConnectionRecord>           connections;
    public                List<EntryIndex>                 entries;
    public                int                              schemaVersion;
}
```

Concrete subclass example, written by the Card Framework integration (any other consumer follows the same shape):

```csharp
// CONSUMER — Card Framework adapter
public sealed class CardEffectGraphAsset : GraphAsset<CardEffectRunner> { }
```

The concrete subclass exists because Unity needs a concrete `ScriptableObject` type per asset (it can't serialize an open generic). It's a one-line file. Designers drag this into `CardEffectGraphAsset` fields; non-card consumers define their own analogous one (e.g. `DialogueGraphAsset : GraphAsset<DialogueRunner>`).

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
- **`ConnectionRecord` is four ints.** `(fromNodeId, fromPortId, toNodeId, toPortId)`. No strings, no indices. Unity serializes them as plain blittable ints inside the SO.
- **Hydration recovers the references.** A `Dictionary<int, RuntimeNode<TRunner>>` indexes the asset's nodes by id; each connection looks up both endpoints by id and produces a typed `Connection<T>` wiring object. See the "Wiring mechanism" subsection under Hydration for the no-reflection mechanics.

That's the durable artifact: the connection between A's port X and B's port Y is **four ints** in the SO. Unity's serializer handles it without any custom pipeline. References get re-resolved fresh on every load.

**Editor positions are not in the runtime asset** — they live in the editor graph (Graph Toolkit handles them). Round-trip editing works because reopening the file shows the editor graph, not the runtime asset.

### Hydration — turning records into a wired runtime tree

This is the step the implementer needs to execute exactly right, because it's where serialized records become a live, port-wired tree the executor can walk. `GraphController<TRunner>.Initialize(runner)` is the entry point.

Pre-condition when `Initialize` is called: the consumer has loaded a `GraphAsset<TRunner>` (Resources, Addressables, direct reference — doesn't matter to the controller). Unity has already deserialized the asset, so:

- `asset.nodes` is a `List<RuntimeNode<TRunner>>` of **live, typed instances** with their inline-value fields populated. No factory step needed.
- `asset.connections` is a `List<ConnectionRecord>` of `(fromNodeId, fromPortId, toNodeId, toPortId)` int tuples.
- `asset.entries` is a `List<EntryIndex>` mapping payload type id → root `nodeId` (int).

Hydration steps, in order:

1. **Index nodes by id.** Build `var byId = new Dictionary<int, RuntimeNode<TRunner>>(asset.nodes.Count);` then `foreach (var n in asset.nodes) byId.Add(n.nodeId, n);`. Int-keyed dictionary, fast hash, no string allocation.

2. **Wire connections.** For each `ConnectionRecord c`:
   ```csharp
   var from = byId[c.fromNodeId];
   var to   = byId[c.toNodeId];
   var conn = from.GetOutputConnection(c.fromPortId);  // virtual call → generated switch on portId
   to.BindInput(c.toPortId, conn);                     // virtual call → generated switch on portId
   ```
   Two virtual calls per connection, each resolving to a compile-time `switch` the source generator wrote. No reflection, no string lookups. Mechanics in "Wiring mechanism" below.

3. **Build the entry index.** `var entryByType = new Dictionary<Type, RuntimeNode<TRunner>>();` Walk `asset.entries`, look up the payload `Type` for each `entryTypeId` in the registry, look up the root node in `byId` by `rootNodeId`, populate the dictionary. This is what `Run<TEntry>(payload)` and `Validate<TEntry>(payload)` look up at dispatch time.

4. **Lifecycle pass.** Walk `asset.nodes` once and:
   - For any node implementing `IInitializableNode<TRunner>`, call `Initialize(runner)`. Nodes can cache typed services off the runner here.
   - For any node implementing `IListenerNode<TRunner>`, collect it into `CommandListeners` for the consumer to register with their pipeline.

5. **Done.** The controller now holds: `byId` (retained for diagnostics / v2 macros — cheap), every input slot on every node populated with a typed `Connection<T>`, the entry-type dictionary, the listener list. After this point the executor walks live C# references and invokes typed delegates. Zero string-keyed lookups, zero reflection, zero dictionary access in the hot path.

#### Wiring mechanism — no reflection

The "lookup" in step 2 is **two virtual calls**, both resolving to switches the source generator wrote at compile time.

For each generated runtime node, the generator emits:

```csharp
// Generated, per payload — example: StrikeDispatcherRuntime
public sealed class StrikeDispatcherRuntime : RuntimeNode<CardEffectRunner> {
    // Generated stable port-ID constants (FNV-1a of field name, or [GraphPort(Id=N)] override).
    // Same constants are used by the baker when translating editor port names to runtime IDs.
    public static class Ports {
        public const int Magnitude    = 0x9F3A_C12B;   // input
        public const int Target       = 0x4D81_77E0;   // input
        public const int DamageDealt  = 0x2C5B_AA09;   // output
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

    public override async ValueTask Execute(CardEffectRunner runner) {
        var mag = _in_Magnitude?.Read() ?? this.Magnitude;   // wired wins; otherwise inline constant
        var tgt = _in_Target   ?.Read() ?? this.Target;
        var result = await runner.EffectScope.Dispatch(new Strike { Magnitude = mag, Target = tgt });
        _out_DamageDealt = result.DamageDealt;
    }
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

public abstract class RuntimeNode<TRunner> where TRunner : GraphRunner {
    public virtual void Initialize(TRunner runner) { }     // optional init hook
    public abstract ValueTask Execute(TRunner runner);
}
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

public sealed class CardEffectGraph : Graph<CardEffectRunner> {
    [MenuItem("Assets/Create/Effects/Card Effect")]
    static void CreateNew() =>
        GraphDatabase.PromptInProjectBrowserToCreateNewAsset<CardEffectGraph>("New Effect");
}

public abstract class CommandDispatcherNode<TCmd, TResult> : RuntimeNode<CardEffectRunner>
    where TCmd : GameCommand<TResult>, new()
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

The editor `Graph<TRunner>` subclass is mandatory (Graph Toolkit registers the file extension via `[Graph("ext")]`). It stays a 5-line class — extension binding + create-asset menu — and the consumer is free to extend it with their own editor concerns. We deliberately do **not** auto-emit the Graph subclass: the boilerplate is small and consumer-extension freedom is more valuable.

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

There is no `IGraphScope` interface in the package. The Card Framework integration creates a `CardEffectRunner` that holds `IEffectScope` as a property; consumer-authored helper bases (e.g., `CommandDispatcherNode`) read from `runner.EffectScope` directly.

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

    // Direct invocation of EntryPoints — payload type drives entry lookup
    public ValueTask        Run<TEntry>(TEntry payload)      where TEntry : EntryPoint;
    public ValueTask<bool>  Validate<TEntry>(TEntry payload) where TEntry : EntryPoint;

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

A complete walkthrough of every file a fresh consumer of this package writes to get one runnable graph. The scenario: a graph with two nodes — an `OnPlay` entry that fires when something plays a card, wired into a `Log` action that prints a message. End-to-end so it's clear what's the consumer's responsibility versus what the package or the generator produces.

### What the consumer writes (9 small files)

#### 1. Domain payload base classes — `MyGame/Domain/Bases.cs`
```csharp
namespace MyGame {
    public abstract class GraphEntry { }
    public abstract class GraphAction { }
}
```
Plain abstract classes. The package never imports these; the generator finds them by name via the assembly attribute.

#### 2. Concrete payloads — `MyGame/Domain/Payloads.cs`
```csharp
namespace MyGame {
    public sealed class OnPlay  : GraphEntry  { public int    CardId;  }   // 1 field → 1 output port
    public sealed class Log     : GraphAction { public string Message; }   // 1 field → 1 input port
}
```
These are the "node types" designers see in the editor's create-node menu. Add a new file here and a new node appears.

#### 3. Runner — `MyGame/Runtime/MyRunner.cs`
```csharp
namespace MyGame {
    public sealed class MyRunner : GraphRunner {
        public ILogger Logger { get; init; }   // host-injected service used by action bases
    }
}
```
Per-execution context. Anything node logic needs at runtime lives here.

#### 4. Helper bases — `MyGame/Runtime/NodeBases.cs`
```csharp
namespace MyGame {
    // Entry nodes don't need much — generator can emit them directly. Skipped.
    // For actions, we provide a base that does the actual domain work:
    public abstract class ActionDispatcher<TAction> : RuntimeNode<MyRunner>
        where TAction : GraphAction, new()
    {
        protected sealed override async ValueTask Execute(MyRunner runner) {
            var action = BuildPayload();   // generator-emitted — reads inputs, populates fields
            await Run(action, runner);
        }
        protected abstract TAction BuildPayload();
        protected abstract ValueTask Run(TAction action, MyRunner runner);
    }
}
```
This is **where the actual behavior lives**. The generator provides typed payload construction; you provide what to do with it.

#### 5. Action behavior — `MyGame/Runtime/LogBehavior.cs`
```csharp
namespace MyGame {
    public sealed class LogBehavior : ActionDispatcher<Log> {
        protected override ValueTask Run(Log action, MyRunner runner) {
            runner.Logger.Log(action.Message);
            return default;
        }
    }
}
```
The generator emits `LogDispatcherRuntime : LogBehavior` (or some equivalent — see "What the generator emits" below). `BuildPayload()` is filled in by the generator, `Run()` is filled in by you.

#### 6. Assembly attribute — `MyGame/AssemblyInfo.cs`
```csharp
[assembly: GraphConfig(
    EntryBases        = new[] { "MyGame.GraphEntry"  },
    CommandBases      = new[] { "MyGame.GraphAction" },
    Convention        = PortConvention.AllFieldsIn,
    RegistryNamespace = "MyGame.Graph.Generated",
    DispatcherBase    = "MyGame.ActionDispatcher`1"
)]
```
Tells the generator what to look for and what helper base to extend.

#### 7. Editor graph subclass — `MyGame/Editor/MyGraph.cs`
```csharp
namespace MyGame.EditorGraph {
    [Graph("mygraph")]
    public sealed class MyGraph : Graph<MyRunner> {
        [MenuItem("Assets/Create/MyGame/Graph")]
        static void CreateNew() =>
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<MyGraph>("New Graph");
    }
}
```
Five lines. Registers the file extension and the create-asset menu item with Graph Toolkit.

#### 8. Asset subclass — `MyGame/Runtime/MyGraphAsset.cs`
```csharp
namespace MyGame {
    public sealed class MyGraphAsset : GraphAsset<MyRunner> { }
}
```
One line. Concrete `ScriptableObject` type so Unity can serialize it (open generics aren't allowed for SOs).

#### 9. Importer subclass — `MyGame/Editor/MyGraphImporter.cs`
```csharp
namespace MyGame.EditorGraph {
    [ScriptedImporter(version: 1, ext: "mygraph")]
    internal sealed class MyGraphImporter
        : GraphAssetImporterBase<MyGraph, MyRunner, MyGraphAsset> { }
}
```
One line. Wires the bake pipeline to the file extension.

#### 10. (game-side) Host MonoBehaviour — `MyGame/Components/GraphHost.cs`
```csharp
namespace MyGame {
    public sealed class GraphHost : MonoBehaviour {
        [SerializeField] MyGraphAsset graphAsset;     // designer drops the .mygraph file here
        GraphController<MyRunner> controller;
        MyRunner runner;

        void Awake() {
            runner = new MyRunner {
                Logger = ServiceLocator.Get<ILogger>(),
                CancellationToken = destroyCancellationToken,
            };
            controller = new GraphController<MyRunner>(graphAsset);
            controller.Initialize(runner);
        }

        public ValueTask Play() => controller.Run(new OnPlay { CardId = 42 });
        void OnDestroy() => controller.Dispose();
    }
}
```
Game-side wiring. Most consumers will have a class like this per gameplay object that uses graphs.

### What the generator emits (consumer never writes any of this)

From `OnPlay : GraphEntry`:
- `MyGame.Graph.Generated.OnPlayEditorNode`  — Graph Toolkit `Node` subclass with one output port `CardId`
- `MyGame.Graph.Generated.OnPlayRuntime`     — `RuntimeNode<MyRunner>` with `Ports.CardId` const, `_out_CardId` field, `BindInput`/`GetOutputConnection` switches
- A registry entry: editor type → runtime type, port-name → port-ID

From `Log : GraphAction` (because `DispatcherBase = ActionDispatcher\`1\``):
- `MyGame.Graph.Generated.LogDispatcherEditorNode` — Graph Toolkit node with input port `Message`
- `MyGame.Graph.Generated.LogDispatcherRuntime : LogBehavior` — concrete subclass that fills in `BuildPayload()` by reading `_in_Message.Read()` (or the inline constant), passes the typed `Log` to `Run()`
- `Ports.Message` const, `[NonSerialized] Connection<string> _in_Message`, the two switch overrides
- `LogListenerEditorNode` + runtime, `LogReturnEditorNode` + runtime (since `Log` is in `CommandBases`, all three node forms are emitted; designer can use whichever they need)
- A registry entry

`Registry` is a generated `static partial class` in `MyGame.Graph.Generated` that the baker reads to translate editor port names into stable port IDs and to look up which runtime class to instantiate per editor node type.

### What the package provides (consumer never writes any of this)

- `GraphRunner`, `Graph<TRunner>`, `RuntimeNode<TRunner>`, `Connection`/`Connection<T>`, `GraphAsset<TRunner>`, `GraphAssetImporterBase<TGraph, TRunner, TAsset>`, `GraphController<TRunner>`, `GraphBaker`, `GraphExecutor<TRunner>`
- Built-in generic nodes (Branch, Cancel, Replace, Return, Return Bool, predicates, math, conversions) — already typed over `RuntimeNode<TRunner>`, work with any consumer's runner
- Source generator (Roslyn incremental) — finds the `[GraphConfig]` attribute, walks `EntryBases`/`CommandBases` descendants, emits per the convention
- Attributes-only DLL: `[GraphConfig]`, `[GraphPort]`, `[GraphHidden]`, `[GraphMenu]`, `[In]`, `[Out]`

### End-to-end at runtime

1. Project compiles → generator produces editor + runtime nodes and the registry → `GraphHost.cs`, `LogBehavior.cs` etc. all reference real types.
2. Designer creates `Foo.mygraph`, double-clicks → Graph Toolkit opens with the create-node menu showing `OnPlay`, `LogDispatcher`, `LogListener`, `LogReturn`, plus all the package-shipped generic nodes.
3. Designer drops `OnPlay`, drops `LogDispatcher`, wires `OnPlay.CardId` to a `[ToString]` builtin, wires that into `LogDispatcher.Message`. Saves.
4. Unity reimports `Foo.mygraph` → `MyGraphImporter` → `GraphBaker` walks the editor graph, assigns stable nodeIds, resolves port names to portIds via the generated registry, emits `MyGraphAsset` SO with three nodes (`OnPlayRuntime`, `ToStringRuntime`, `LogDispatcherRuntime`) and two `ConnectionRecord`s. The asset is `SetMainObject` — what other assets reference.
5. Designer drops `Foo.mygraph` onto `GraphHost.graphAsset` field on a prefab.
6. Game runs, `Awake()` fires → `controller.Initialize(runner)` → hydration walks connections, builds `Connection<int>` and `Connection<string>` instances, wires them into the right runtime-node slots via the generated `BindInput` switches.
7. `Play()` is called → `controller.Run(new OnPlay { CardId = 42 })` → executor finds the entry root in the entry-type dictionary, runs it → `OnPlayRuntime` writes `_out_CardId = 42`, advances flow → `ToStringRuntime` reads its input via `_in_Value.Read()`, writes its output → `LogDispatcherRuntime.Execute(runner)` calls `BuildPayload()` (reads `_in_Message.Read()` → "42"), constructs `new Log { Message = "42" }`, hands it to `LogBehavior.Run` → `runner.Logger.Log("42")`.

That's the complete vertical slice. **10 files, ~80 lines of consumer code total**, plus whatever payload classes the consumer adds over time. Everything else is generator output, package code, or designer-authored graph files.

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

Implemented via Graph Toolkit's `OnGraphChanged(GraphLogger)` hook on the consumer's `Graph<TRunner>` subclass. The package ships shared validation helpers the consumer's override can call, but the hook itself lives on the consumer-defined graph class. Each rule logs to the supplied logger:

- All entry/listener Run flows must end at a valid terminator
- Validate flows must end at `[Return Bool]` (if connected at all)
- Required input ports must be wired or carry a constant
- Hand-written generic nodes provide their own validators
- Generated nodes inherit a base validator with default rules

### Bake-time (ScriptedImporter)

- Detect cycles in flow paths (other than legitimate trigger registration cycles, which aren't graph-internal)
- Validate type compatibility between connected ports
- Emit the consumer's concrete `GraphAsset<TRunner>` subclass only if no errors; on error, log via `ctx.LogImportError` and skip emission

### Runtime

- Executor propagates exceptions; consumer's pipeline catches and decides
- Cancellation via `runner.CancellationToken` aborts the walk cleanly
- Diagnostics / telemetry hooks (if needed) live on the consumer's runner — package defines no event surface in v1

---

## Phasing

### Milestone 1 — Foundation (1 week)

- Project structure: package layout, asmdefs, package manifest
- Attributes-only DLL: `[In]`, `[Out]`, `[GraphHidden]`, `[GraphPort]`, `[GraphMenu]`, `[NoGraphNode]`
- `GraphConfig` attribute and convention enum
- Generator skeleton: read config, locate base-type descendants, emit a stub file per type
- One end-to-end test: a trivial `EntryPoint` → walks through the generator → emits a node class → loads in Graph Toolkit

### Milestone 2 — Generator (1.5 weeks)

- `CommandResultPair` convention strategy
- Per-payload emission: editor Dispatcher / Listener / Return nodes
- Per-payload emission: runtime nodes
- Per-payload `Ports` constants + generated `BindInput` / `GetOutputConnection` switch overrides
- Registry partial-class emission
- Diagnostics: EFG001 (config required), EFG005 (missing result pair)
- `AttributedFields` convention (validates the generator's strategy abstraction)

### Milestone 3 — Asset lifecycle + runtime (1.5 weeks)

- `GraphRunner` abstract base class
- `Graph<TRunner>` package base for consumer editor-graph subclasses
- `RuntimeNode<TRunner>`, `IInitializableNode<TRunner>`, `IListenerNode<TRunner>` interfaces
- `GraphAsset<TRunner>` abstract runtime `ScriptableObject` base + record types (`ConnectionRecord`, `EntryIndex`); consumer ships a one-line concrete subclass like `CardEffectGraphAsset : GraphAsset<CardEffectRunner>` (no variables collection in v1)
- `GraphAssetImporterBase<TGraph, TRunner>` — package-shipped reusable importer base
- `GraphBaker` — walks nodes/edges, extracts port values via `port.firstConnectedPort` / `port.TryGetValue`, emits records, validates (no variable-name resolution in v1)
- `GraphExecutor<TRunner>` async tree walker
- `GraphController<TRunner>` API surface
- End-to-end test (consumer-side smoke project): create `.cardgraph` → edit → re-import → hydrate → run → assert side effects

### Milestone 4 — Editor authoring (1.5 weeks)

- Hand-written generic nodes (Cancel, Replace, Return, Return Bool, Branch, predicates, math, conversions) — generic over `TRunner`
- Validate / Run flow port handling on generated entry / listener nodes
- `[Return Strike]` (and friends) typed terminator with unwired-default semantics
- `OnGraphChanged` edit-time validation rules
- (No blackboard panel — deferred to v2)

### Milestone 5 — Card Framework integration (1 week)

- `CardEffectRunner : GraphRunner` with `IEffectScope`, host references, services
- `CardEffectGraph : Graph<CardEffectRunner>` editor subclass + `[ScriptedImporter]` importer subclass
- Helper bases: `CommandDispatcherNode<TCmd, TResult>`, `CommandListenerNode<TCmd, TResult>`, `ReturnPayloadNode<TCmd>`, `EntryPointNode<TEntry>` — all `RuntimeNode<CardEffectRunner>`
- `ICommandListener<CardEffectRunner>` adapter to the framework's pipeline registration interface
- One real card authored end-to-end (recreate `500 Strike` from Overknights)
- Documented integration recipe

### Milestone 6 — Polish (1 week)

- Remaining built-in conventions (`MutableInReadOnlyOut`, `AllFieldsIn`)
- Diagnostics EFG002–EFG006
- Per-type escape-hatch attribute handling
- Editor menu organization (`[GraphMenu]`)
- Documentation: package README, conventions guide, "writing a payload" tutorial

**Total estimate:** ~7-8 weeks of focused work for v1.

---

## v2 follow-ups

Tracked but explicitly out of scope for v1.

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
2. `**NodeRecord.typeId` is namespaced.** Reserve a prefix for macro references (e.g., `macro:guid:...`). Generator-emitted types use a different prefix.
3. **Asset shape is generic over node payload.** No per-type asset records. New node categories (macro reference, sub-graph call) extend the registry, not the asset format.
4. **Controller listener API uses an interface, not a concrete listener class.** Macro-as-listener (a macro that registers triggers) becomes a different `ICommandListener<TRunner>` implementation later.
5. **Convention strategies are sealed but isolated.** Each strategy is its own class behind an internal interface; v2's pluggable extension just opens the interface.

---

## Reference: what we're explicitly NOT doing (lessons from v1)


| Anti-pattern from old `Assets/GraphFlow/`           | Why we're not repeating it                                                                                                             |
| --------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| Per-type editor `Node` classes hand-written         | Generator emits them; consumer writes payloads only                                                                                    |
| Per-type runtime definitions hand-written           | Same                                                                                                                                   |
| Baker `switch (node)` pattern-matching              | Type-driven dispatch via registry                                                                                                      |
| Hardcoded blackboard wiring seeds                   | No blackboard in v1; payload-port wiring via generated accessors, host references via consumer-authored runner-property accessor nodes |
| Reflection on field names per execution             | Typed `Connection<T>` slots wired at hydration via generated port-ID switches; hot path is a direct delegate invoke                     |
| Entry type stored as `AssemblyQualifiedName` string | Stable type id from registry; bake-time validated                                                                                      |
| Linear-flow-only with no branching                  | `Branch` node and Validate flow are first-class                                                                                        |
| Dead `IGraphTickService`                            | No tick service; consumer drives lifecycle                                                                                             |
| Vague blackboard inheritance for sub-graphs         | No sub-graphs in v1; v2 design starts from explicit semantics                                                                          |
| Source generator that doesn't ship                  | Generator is committed v1 scope; nothing depends on a future generator                                                                 |


---

## Open questions for implementation phase

To resolve once we start building:

All previously-open architecture questions are resolved:


| Topic                           | Resolution                                                                                                                                                                                                                                                                                                                                                     |
| ------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Blackboard / variables          | **No blackboard in v1.** Designer constants as inline embedded port values. Host-injected references on the consumer's runner. Full blackboard deferred to v2.                                                                                                                                                                                                 |
| Runtime context                 | `**GraphRunner` abstract base class, generic `RuntimeNode<TRunner>`.** Consumer subclasses `GraphRunner` with their own services. Package has no opinion on what's on the runner.                                                                                                                                                                              |
| Editor `Graph` subclass         | **Mandatory, hand-written by consumer (~5 lines).** Not auto-generated; preserves consumer freedom to extend.                                                                                                                                                                                                                                                  |
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

1. Runtime nodes are stored as typed `[SerializeReference]` instances on the asset (`GraphAsset<TRunner>` abstract base, with consumer-defined concrete subclass). We do **not** store a `byte[] serializedConstants` blob — the source generator already produces a typed class per payload, so the asset can carry those instances directly and let Unity serialize their fields the normal way. This is what keeps Unity-object references (Cards, Sprites, AssetReferences, ScriptableObjects on the runner side) patch-correct through the build pipeline.
2. **VND only validates the asset-shipping story, not the connection model.** VND is a linear sequence (start node + `next` chain) baked into a flat list — it has no port-to-port wiring. Our connection model follows the FlowCanvas pattern instead: stable integer IDs (never indices) on both endpoints, `ConnectionRecord` carrying `(fromNodeId, fromPortId, toNodeId, toPortId)` as four ints, and load-time resolution that produces typed `Connection<T>` wiring objects via two virtual calls per edge — both resolving to compile-time switches the source generator wrote. See "Connection model", "Hydration", and "Wiring mechanism" sections for the explicit walkthrough.

---

*End of plan.*