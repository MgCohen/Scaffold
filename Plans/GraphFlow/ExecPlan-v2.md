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
│  • Declares [assembly: EffectGraphConfig(...)]                     │
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
│               │    │  │  • EffectGraphController<TRunner>         │
│               │    │  │  • GraphExecutor<TRunner> (tree walker)   │
│               │    │  │  • IInitializableNode<TRunner> / IListenerNode<TRunner> │
│               │    │  │  • EffectGraphAsset ScriptableObject      │
│               │    │  └─ Generator assembly (Roslyn)              │
│               │    │     • [assembly: EffectGraphConfig] reader   │
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
                  │  • Accessor delegates        │
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
[assembly: EffectGraphConfig(
    CommandBases = new[] { "MyGame.GameCommand`1" },
    EntryBases   = new[] { "MyGame.EntryPoint" },
    Convention   = PortConvention.CommandResultPair,
    RegistryNamespace = "MyGame.Effects.Generated"
)]
```

- **Required**. Compile error `EFG001` if missing.
- `CommandBases` and `EntryBases` are lists — supports multiple base types per category (e.g., separating `GameAction` from `GameSignal`).
- Base type names are fully-qualified strings; generator does syntax-only matching, no compilation-side type loading.

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
- 3 runtime node classes (matching counterparts)
- Accessor delegate table (one `Action<T, V>` setter and `Func<T, V>` getter per field)
- One `EffectRegistry` partial-class entry

For an `EntryPoint` subclass with N fields:

- 1 editor `Node` subclass (the entry node)
- 1 runtime node class
- Accessor delegate table
- One registry entry

### Resolved generator behavior


| Decision                    | Resolution                                          |
| --------------------------- | --------------------------------------------------- |
| Config required             | Yes; missing → `EFG001`                             |
| Conventions per assembly    | One only (split assemblies if mixed needed)         |
| Abstract bases              | Skipped automatically, only instantiable types emit |
| Open generics               | Not supported in v1; emits warning if encountered   |
| Registry shape              | `static partial class` so consumers can extend      |
| Nullable types              | Pass through as-is                                  |
| Field ordering              | Source order; override via `[GraphPort(Order = N)]` |
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

Following Unity's `VisualNovelDirector` sample: **one source file on disk** (with an extension the consumer picks — e.g. `.cardgraph` for the Card Framework integration) holds both the editor graph (hidden, for re-editing) and the imported runtime asset (the `SetMainObject`, what consumers reference). Designers drag the file into a `EffectGraphAsset` field and get the runtime form directly.

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

1. **Node translation.** Each editor node maps to one or more runtime node records by `typeId`. Most map 1:1 (`StrikeDispatcherNode` editor → runtime record). Some may be 1:N (a single editor node compiling to multiple runtime steps — VND-style — if a built-in node ever needs that).
2. **Connection resolution.** Editor port-to-port connections (between editor node instances) are resolved into runtime `ConnectionRecord`s (between runtime node ids by stable port index). Connections involving editor-only helper nodes are inlined where appropriate, not preserved literally.
3. **Embedded value extraction.** Each unwired input port's constant value is read via `port.TryGetValue<T>()` and packed into the runtime record's `serializedConstants` blob.
4. **Bake-time validation.** Errors that prevent runtime correctness are reported here, even if the editor accepted them:
  - Required ports unwired and lacking a constant
  - Type mismatches Graph Toolkit didn't catch
  - Missing terminators on non-void Run flows (`[Return X]` / `[Cancel]` / `[Replace]`)
  - Cycles in flow paths
  - Entry node referring to an `EntryPoint` type that no longer exists in the registry
  - Listener node referring to a `GameCommand` type that's been removed
5. **Entry-point indexing.** Walks all entry/listener nodes, builds the `entryIndex` table mapping bound `Type` to the runtime root node id.
6. **Schema version stamping.** The runtime asset records the current schema version (paired with the importer version — see asset versioning below).

A graph that passes `OnGraphChanged` validation can still **fail to bake**. That's fine and expected — `OnGraphChanged` runs on every keystroke and does cheap structural checks; the baker does deeper analysis (whole-graph cycles, registry lookups, type cross-validation). On bake failure, no runtime asset is emitted; the source file becomes "edits but doesn't build" and the Inspector / Console explains why.

### Runtime asset format

The runtime asset is a real Unity `ScriptableObject` — added as a sub-asset and made the main object via `ctx.SetMainObject` (same pattern as the VisualNovelDirector sample). That means: it survives builds, ships through Addressables, can be referenced from prefabs and scenes by GUID, shows up in the Inspector. **It is an asset, not a serializable POCO.** The editor graph stays inside the same source file as a non-main object; nothing at runtime touches it.

Serialization is **plain Unity ScriptableObject serialization** — we ride Unity's machinery, we do not layer a custom one on top:

```csharp
public sealed class EffectGraphAsset : ScriptableObject {
    [SerializeReference] public List<RuntimeNode>            nodes;        // typed, polymorphic
    public                List<RuntimeConnectionRecord>      connections;
    public                List<EntryIndex>                   entryIndex;
    public                int                                schemaVersion;
}

[Serializable]
public abstract class RuntimeNode {
    public string nodeId;             // stable guid (consistent across re-imports)
    // generated subclasses carry their typed inline-value fields directly:
    //   public sealed class StrikeDispatcherRuntime : RuntimeNode<CardEffectRunner> {
    //       public int   Magnitude;
    //       public Card  Target;
    //       ...
    //   }
}

[Serializable]
public struct RuntimeConnectionRecord {
    public string fromNodeId;
    public int    fromPortIndex;       // resolved at bake time, not edit-time port name
    public string toNodeId;
    public int    toPortIndex;
}
```

Why typed `[SerializeReference]` nodes instead of an opaque `byte[] serializedConstants` blob: the source generator already emits a runtime class per payload type, so each node has a known C# shape. Storing that shape directly means

- Unity serializes fields normally — `Sprite`, `AssetReferenceT<>`, `UnityEngine.Object` references all patch up correctly through the build pipeline (the VND sample relies on this for `BackgroundSprite`).
- The runtime asset is Inspector-debuggable.
- Hydration reads already-typed objects; no per-node `Deserialize(blob)` step, no `typeId` string lookup at load time (the C# type *is* the identity).
- No hand-written serializer/deserializer to maintain.

`typeId` strings still exist, but only at the **registry/bake** layer — they're how the baker maps a Graph Toolkit editor node to the runtime node class to instantiate. Once instantiated and stored in the asset, the runtime carries the live typed instance.

No `variables` collection in v1 — the blackboard concept is deferred. Designer constants are baked into the corresponding fields on the typed runtime nodes; host-injected references come off the consumer's runner. When v2 adds a blackboard, we'll add a `variables` field and bump `schemaVersion` + the importer version.

Two concrete differences from the editor connections:

- **Port references are integer indices**, not names. The baker maps editor port names (designer-visible labels) to integer indices (registry-stable, source-gen-determined). Faster lookup, immune to editor-side label rename if we ever support it.
- **Connection records reference runtime node ids only.** Editor-only helper nodes that get inlined or dropped during bake do not appear.

**Editor positions are not in the runtime asset** — they live in the editor graph (Graph Toolkit handles them). Round-trip editing works because reopening the file shows the editor graph, not the runtime asset.

### Hydration

At game time, `EffectGraphController<TRunner>.Initialize(runner)` constructs the in-memory runtime tree from the asset. Because nodes are stored as typed `[SerializeReference]` instances with their inline-value fields already populated by Unity, hydration is mostly **wiring**, not reconstruction:

1. The `nodes` list is already a list of live, typed `RuntimeNode<TRunner>` subclass instances — Unity deserialized them when the asset loaded. No registry lookup, no blob deserialization at this point.
2. Build a `Dictionary<string, RuntimeNode<TRunner>>` keyed by `nodeId` for connection resolution.
3. For each `RuntimeConnectionRecord`, set up the port-binding delegate from the source node's output to the target node's input. Delegates come from the source-generated accessor tables — no reflection.
4. Build the entry-point lookup: `Dictionary<Type, RuntimeNode<TRunner>>` for fast `Invoke<T>` dispatch.
5. Walk the tree once calling `Initialize(runner)` on `IInitializableNode<TRunner>` instances; collect `IListenerNode<TRunner>` instances for the consumer.

Hydration is paid once per controller. After it, the executor walks pointer references and invokes delegates — no string-keyed lookups in the hot path.

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

### `EffectGraphController<TRunner>`

The controller is generic over the consumer's runner type — the same one that types nodes:

```csharp
public sealed class EffectGraphController<TRunner> where TRunner : GraphRunner {
    public EffectGraphController(EffectGraphAsset asset);

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
    EffectGraphController<CardEffectRunner> graph;
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

- `EffectGraphController<TRunner>.Initialize(runner)` walks the runtime tree once and calls `Initialize(runner)` on any `IInitializableNode<TRunner>`. Nodes can fetch typed services from the runner here and cache them.
- `EffectGraphController<TRunner>.RefreshListeners()` walks the tree and calls `RefreshSubscriptions(runner)` on any `IListenerNode<TRunner>`. Used when the host wants to re-evaluate listener activity (e.g., on zone change). Actual pipeline registration stays consumer-driven; this hook just updates internal node state.

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
- Emit `EffectGraphAsset` only if no errors; on error, log via `ctx.LogImportError` and skip emission

### Runtime

- Executor propagates exceptions; consumer's pipeline catches and decides
- Cancellation via `runner.CancellationToken` aborts the walk cleanly
- Diagnostics / telemetry hooks (if needed) live on the consumer's runner — package defines no event surface in v1

---

## Phasing

### Milestone 1 — Foundation (1 week)

- Project structure: package layout, asmdefs, package manifest
- Attributes-only DLL: `[In]`, `[Out]`, `[GraphHidden]`, `[GraphPort]`, `[GraphMenu]`, `[NoGraphNode]`
- `EffectGraphConfig` attribute and convention enum
- Generator skeleton: read config, locate base-type descendants, emit a stub file per type
- One end-to-end test: a trivial `EntryPoint` → walks through the generator → emits a node class → loads in Graph Toolkit

### Milestone 2 — Generator (1.5 weeks)

- `CommandResultPair` convention strategy
- Per-payload emission: editor Dispatcher / Listener / Return nodes
- Per-payload emission: runtime nodes
- Accessor delegate table emission
- Registry partial-class emission
- Diagnostics: EFG001 (config required), EFG005 (missing result pair)
- `AttributedFields` convention (validates the generator's strategy abstraction)

### Milestone 3 — Asset lifecycle + runtime (1.5 weeks)

- `GraphRunner` abstract base class
- `Graph<TRunner>` package base for consumer editor-graph subclasses
- `RuntimeNode<TRunner>`, `IInitializableNode<TRunner>`, `IListenerNode<TRunner>` interfaces
- `EffectGraphAsset` runtime `ScriptableObject` with the record types (no variables collection in v1)
- `GraphAssetImporterBase<TGraph, TRunner>` — package-shipped reusable importer base
- `GraphBaker` — walks nodes/edges, extracts port values via `port.firstConnectedPort` / `port.TryGetValue`, emits records, validates (no variable-name resolution in v1)
- `GraphExecutor<TRunner>` async tree walker
- `EffectGraphController<TRunner>` API surface
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
| Reflection on field names per execution             | Cached delegates from accessor table                                                                                                   |
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

**Implication for our plan:** runtime nodes are stored as typed `[SerializeReference]` instances on `EffectGraphAsset`. We do **not** store a `byte[] serializedConstants` blob — the source generator already produces a typed class per payload, so the asset can carry those instances directly and let Unity serialize their fields the normal way. This is what keeps Unity-object references (Cards, Sprites, AssetReferences, ScriptableObjects on the runner side) patch-correct through the build pipeline.

---

*End of plan.*