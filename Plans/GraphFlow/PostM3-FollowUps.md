# Post-M3 Follow-Ups

Decisions captured after M3 closed and the user surfaced 9 problems with the implementation that didn't match what was discussed/decided. Each decision feeds into the implementation plan at [PostM3-ImplPlan.md](PostM3-ImplPlan.md).

This doc is the rationale anchor — it answers "why did we do it this way?" for future readers.

## Implementation status

All seven decisions implemented across six phases.

| # | Title | Phase | Commit |
| --- | --- | --- | --- |
| 1 | Drop TResult from entries | 1 | `9b42a63` |
| 2 | Drop `[GraphCommandPair]` attribute | 4 | `7eacac6` |
| 3 | `IGraphEntry` + `OnTrigger<TEvent>` primitive | 1 (markers) + 3 (OnTrigger) | `9b42a63`, `4543dd8` |
| 4 | Port IDs as strings | 2 | `edfefa5` |
| 5 | Two-tier RuntimeNode hierarchy | 2 | `edfefa5` |
| 6 | CardSandbox file hygiene | 5 | `a6a95ea` |
| 7 | NaughtyAttributes — live with it | — (no code) | n/a |

Verified: all 7 GraphFlow tests pass (5 M0 + 2 CardSandbox), 14 snapshot baselines stable, Unity batchmode compile zero `error CS` lines.

---

## Decision #1 — Drop `TResult` from entries (revert D5/D9 typed Run)

### Problem

`EntryRuntimeNode<TEntry, TRunner, TResult>`, `IGraphEntry<TPayload, TResult>` (+ 1-arg sugar), and `controller.Run<TEntry, TResult>(payload) → Task<TResult>` were introduced in M3 phase 3 per plan decisions D5 + D9. The TResult parameter forces:

- A third type token through every entry runtime + emit + registration site.
- Reflective `MakeGenericMethod` + `MethodInfo.Invoke` in `GraphController.Initialize` to close `BindEntry<TEntry, TResult>` per entry node.
- Reflective `SetPayload` invocation in `GraphController.RunFlow<TEntry>` per run — a hot-path violation of plan goal #7 ("zero runtime reflection on hot paths").

In current M3, **no test or sample call site uses the typed `Run<TEntry, TResult>` API**. Every M0 + CardSandbox test uses `RunFlow<TEntry>(payload) → Task<Flow>` and reads `flow.ReadResult<T>()` when needed. The TResult ergonomics surface is dead code; the reflection cost is real.

### Why it happened

D5 was forward-investment for a future where entries return rich typed values (`DealStrike → DamageResult`, etc.) and callers want them inline. The pattern in M3 turned out different — every entry returns Unit, and every caller wants `flow.Outcome` alongside the result, which `RunFlow` exposes natively. The future that needed TResult didn't arrive in v1.

The reflection in `GraphController` was the agent's coping mechanism for "how does the controller, which doesn't statically know TEntry/TResult, dispatch into a typed `EntryRuntimeNode<,,>`?" The question only exists because TResult was added.

### Options considered

**(a) Keep TResult, replace reflection with generator-emitted typed bridges.** Generator emits a per-entry `IEntryBridge` implementation; controller uses a `Dictionary<Type, IEntryBridge>`. Zero reflection, strongly typed end-to-end. Cost: extra generator emit per entry (~10–15 lines), TResult complexity stays.

**(b) Drop TResult.** `EntryRuntimeNode<TEntry, TRunner>`, `IGraphEntry<TPayload>`, `controller.Run<TEntry>(payload) → Task<Flow>`. Reflection problem evaporates because the controller only dispatches on TEntry. `Return<TRunner, TResult>` stays as a built-in node — its TResult is consumer-facing (what the user picks in the visual picker), not framework-facing. Callers read `flow.ReadResult<T>()` for typed values.

**(c) Non-generic `IEntryNode` interface.** Boxing every payload + return at the dispatch boundary. Avoided.

**(d) Cache reflection.** Band-aid; doesn't fix AOT/IL2CPP fragility of `MakeGenericMethod`.

### Decision

**(b) — drop TResult from entries.**

### Rationale

- Plan's three original justifications for TResult (typed Run ergonomics, EFG-V07 framing, three-token pattern matching) all survive without TResult on entries:
  - Typed Run is unused.
  - EFG-V07 can be reframed as "all Return nodes in a graph agree on TResult" enforced at the Return nodes themselves at edit-time, not against an entry's TResult.
  - Pattern-match discrimination keys on TPayload, not TResult — `EntryRuntimeNode<DamageDealtEvent, CardEffectRunner>` is the same disambiguator with one fewer token.
- ReadResult boxing of Unit / bool / int is negligible — `controller.Run` is called per entry invocation (one-per-card-play), not per inner-loop step. User explicitly accepted this cost.
- Eliminates the reflection problem (#1 of the post-M3 issue list) at the source rather than fixing it with more codegen.
- Reduces type-parameter count through every entry-related generator emit site.

### Consequences

- `EntryRuntimeNode<TEntry, TRunner>` (back to 2 type params).
- `IGraphEntry<TPayload>` (no TResult arg, no sugar overload needed).
- `IGraphTrigger<TEvent> : IGraphEntry<TEvent>` (still works).
- `controller.Run<TEntry>(payload) → Task<Flow>` is the single Run API (replaces both `Run<TEntry, TResult>` and `RunFlow<TEntry>`).
- `Unit` struct can stay (used by `Return<R, Unit>` for void-ish flows) or be replaced by skipping the Value port on a default Return — implementer's call when this lands.
- `Return<TRunner, TResult>` built-in node unchanged.
- EFG-V07 reframes: walk Return nodes in graph, error if their TResult args disagree (no entry TResult to compare against).
- Generator emit simplification: `EmitEntryRuntime` drops the result-type lookup; payload discovery walks `IGraphEntry<,>` interface as `IGraphEntry<>`.
- Sandbox payload migration: `IGraphEntry<OnPlay>` shape stays unchanged for users (the 1-arg form was already the sugar).

---

## Decision #2 — Drop `[GraphCommandPair]` attribute

### Problem

To make a Mode-2 command discoverable by the generator, the user currently writes:

```csharp
[GraphCommandPair(
    ResultType = typeof(Unit),
    FlowInPortId = unchecked((int)0xC1D0_0001u),
    FlowOutPortId = unchecked((int)0xC1D0_0002u))]
public sealed class DealDamageCommand : Command<Unit> { ... }
```

`ResultType = typeof(Unit)` re-declares what `Command<Unit>` already says. Generator could read the result type directly from the closed base class via Roslyn. The attribute exists primarily as a coping mechanism for an M1-era Roslyn quirk: cross-assembly `typeof(...)` reads in attribute metadata sometimes return `Kind=Error` because the C# compiler omits assembly qualifiers for same-asm type names. The agent worked around it with `PayloadDiscovery.FindResultTypeFromDispatcherSubclass` (the fallback walker). Net result: attribute = primary path, base-type walk = fallback. The roles should swap.

The flow port IDs are also explicit hex literals — separate concern threaded as #3.

### Why it happened

`[GraphCommandPair]` was introduced in M1 to opt commands into Mode-2 emit when cross-asm attribute metadata reads were flaky. The fallback walker arrived later; the attribute stayed as the primary surface and was never re-evaluated.

### Options considered

**(a) Drop `[GraphCommandPair]` entirely.** Generator discovers commands by walking the package asm for any class assignable to `[GraphPackage].CommandBase` (closed at the package's runner). Result type read from the closed `Command<TResult>` base directly via Roslyn — no `typeof(...)` round-trip, no flakiness. Flow port IDs derived (#3).

**(b) Keep `[GraphCommandPair]` as opt-in only.** Useful for explicit overrides; commands without it still work. Backward-compat-friendly but keeps the noise.

**(c) Rename + simplify.** `[GraphCommand]` with no required args. Same as (a) but flagged at the type — gives consumers a place to add per-command overrides later.

### Decision

**(a) — drop `[GraphCommandPair]` entirely.**

### Rationale

- The attribute's information is fully derivable from the type signature (`Command<Unit>` already says result is Unit).
- Discovery via `CommandBase` walk + Roslyn base-type inspection avoids the M1 cross-asm `typeof()` flakiness — Roslyn's own type system is the authority, not attribute metadata.
- Removes per-command boilerplate — visually authoring a card means writing `public sealed record DealDamageCommand : Command<Unit> { public int Amount; }` and that's it.
- Aligns with the plan's spirit ("zero ceremony for the common path").

### Consequences

- `GraphCommandPairAttribute` removed from AttributesLib.
- `PayloadDiscovery.TryReadCommandPairAttribute` becomes the secondary path / removed; `FindResultTypeFromDispatcherSubclass` (or a simpler "read TResult from `Command<>` base") becomes the primary discovery.
- Flow port IDs the attribute carried (FlowInPortId, FlowOutPortId) need to be derived — handled by decision #3.
- CardSandbox `DealDamageCommand` simplifies to `public sealed record DealDamageCommand : Command<Unit> { public int Amount; }`.
- Snapshot baselines unchanged (M0 sandbox doesn't use `[GraphCommandPair]`).

---

## Decision #3 — Marker hierarchy + built-in `OnTrigger<TEvent>` primitive

### Problem

Three intertwined complaints surfaced together:
1. Marker self-reference (`OnPlay : IGraphEntry<OnPlay>`, `PreDamageDealtEvent : IGraphTrigger<PreDamageDealtEvent>`) is awkward.
2. `EntryRuntimeNode<TEntry, TRunner>` doesn't share any base interface with the marker — runtime base + authoring marker live in disconnected type systems.
3. Per-event-type trigger entry classes (one editor node per event class per phase) are heavy: `PreDamageDealtEvent` and `DamageDealtEvent` are two distinct trigger nodes when conceptually they're "OnDamageDealt with phase choice".

The current shape forces every event class to author itself + its own trigger class + its phase counterpart, all duplicated through the generator emit pipeline.

### Why it happened

D5 chose TPayload-as-self for the marker because it threaded the payload type through the marker for controller dispatch with minimum mechanism. ITrigger inherited the same shape. Per-event editor mirrors fell out of "triggers are entries" + per-payload emit. Self-reference + per-event-class explosion were the costs; never re-evaluated.

### Decision

**A unified design that resolves problems #2 (markers) + #7 (Pre/Post nodes) + #8 (single OnEvent node) at once.**

```csharp
// Marker — empty, non-generic, base for any entry payload.
public interface IGraphEntry { }

// Imperative entries — user-written classes implementing the marker.
public sealed class OnPlay : IGraphEntry
{
    // Typed input fields the graph reads via output ports.
}

// Triggers — single framework-shipped generic primitive. Users do NOT write
// per-event trigger classes; they pick OnTrigger<T> from the visual picker
// (T comes from an option dropdown listing all [GraphEvent]-tagged types).
public sealed class OnTrigger<TEvent> : IGraphEntry
{
    public TEvent? Event;     // payload (set by host at subscription time)
    public Timing Timing;      // Before / After — dropdown in the editor
}

public enum Timing { Before, After }

// New attribute that opts an event class into the OnTrigger picker dropdown.
[AttributeUsage(AttributeTargets.Class)]
public sealed class GraphEventAttribute : Attribute { }

// Runtime base implements IGraphEntry so types unify across authoring + runtime.
public abstract class EntryRuntimeNode<TEntry, TRunner> : RuntimeNode<TRunner>, IGraphEntry
    where TEntry : class
    where TRunner : GraphRunner { ... }
```

### Rationale (per problem)

- **Self-reference (#2)** — gone for both kinds. `OnPlay : IGraphEntry` is clean. `OnTrigger<DamageDealt>` is a built-in generic; user instantiates it through the picker, never writes `: IGraphEntry<self>` themselves.
- **Pre/Post duplication (#7)** — `OnTrigger<DamageDealt> { Timing = Before }` and `... { Timing = After }` are the same node type with different option choices. One picker entry, one editor mirror, one runtime class. Phase becomes a dropdown.
- **One OnEvent node (#8)** — `OnTrigger<TEvent>` is the type-parameterized version. Picker entry is "OnTrigger" once; T comes from a dropdown of `[GraphEvent]`-tagged types. (Q1 below.)

### Q1 — Editor implementation: dynamic options, not closed instantiations

GraphToolkit supports option-driven dynamic port definition via `OnDefineOptions` + `OnDefinePorts` reading option values:

```csharp
[Serializable]
public sealed class OnTriggerEditorNode : Node
{
    protected override void OnDefineOptions(IOptionDefinitionContext context)
    {
        context.AddOption<TypeReference>("EventType");    // dropdown of [GraphEvent]-tagged types
        context.AddOption<Timing>("Timing");              // Before / After dropdown
    }

    protected override void OnDefinePorts(IPortDefinitionContext context)
    {
        context.AddOutputPort("FlowOut").WithConnectorUI(PortConnectorUI.Arrowhead).Build();

        var typeOption = GetNodeOptionByName("EventType");
        if (!typeOption.TryGetValue<TypeReference>(out var typeRef) || typeRef?.Type == null) return;

        // For each [GraphPort] field on the event class, add a typed output port.
        foreach (var f in GraphEventRegistry.GetPortFields(typeRef.Type))
            context.AddOutputPort(f.Type, f.Name).Build();
    }
}
```

**Decision: dynamic editor (Path II), not closed-instantiations (Path I).**

- One editor mirror for all events instead of N. Picker doesn't bloat with per-event nodes.
- `TypeReference` (from Scaffold.Types) provides a strongly-typed picker dropdown, not a string.
- `GraphEventRegistry` — generator already walks the package asm and emits per-package registries. One more table (`EventTypes` with port-field metadata per `[GraphEvent]`-tagged class) costs nothing extra; reuses the existing emit pipeline. Free caching.
- Avoids the M3-deferred multi-T closure-expansion mechanism — `OnTrigger<T>` constructs at hydration via reflection (one-time, not hot path), driven by the option value.

### Q2 — Bus subscription: host territory, not framework

Framework ships:
- `OnTrigger<TEvent>` primitive with `Timing` field.
- `Timing` enum.
- `EntryNodes` catalog on `GraphController`.

Framework does NOT ship:
- Any event bus implementation.
- Any subscription mechanics.
- Any opinion on how Before/After are wired (single event vs split events).

Hosts pattern-match `EntryNodes`, find `EntryRuntimeNode<OnTrigger<X>, R>` instances, and wire them into whatever bus they own according to whatever phase semantics they want. Sample CardSandbox demonstrates one shape (probably collapsing PreDamageDealt + DamageDealt into a single DamageDealt event published twice with Timing). The sample's choice doesn't constrain the framework.

### Consequences

**Renamed / removed types:**
- `IGraphEntry<TPayload, TResult>` → removed (D5).
- `IGraphEntry<TPayload>` (the 1-arg sugar) → removed.
- `IGraphTrigger<TEvent>` → removed.
- New: `IGraphEntry` (non-generic empty marker).

**New types:**
- `OnTrigger<TEvent>` (package runtime — built-in primitive).
- `Timing` enum (package runtime).
- `[GraphEvent]` attribute (AttributesLib — opts an event class into the OnTrigger dropdown).

**Generator:**
- Stops emitting per-event-type trigger editor mirrors / runtimes.
- Emits one new package-level table: `<Stem>GraphRegistry.EventTypes` — list of `(TypeReference, IReadOnlyList<PortFieldMeta>)` entries built from `[GraphEvent]` walk of the package asm.
- `OnTriggerEditorNode` ships in the package's editor asm (hand-written, not generator-emitted) — single mirror serves all events.
- Hydration constructs `OnTrigger<T>` from the editor node's serialized option (one-time reflection at hydration, not hot path).

**Sandbox migration:**
- M0 sandbox: `OnPlay : IGraphEntry<OnPlay>` → `OnPlay : IGraphEntry`. Trivial.
- CardSandbox: `PreDamageDealtEvent` + `DamageDealtEvent` collapse into one `[GraphEvent] DamageDealt` class; trigger nodes become `OnTrigger<DamageDealt>` with Timing dropdown. Sample's bus shape decides Pre/Post mechanics (sample-side, not framework).

**`object? Target` cleanup:** the `OnPlay.Target` and `DamageDealt.Target` fields currently typed as `object?` are sample-level placeholder noise — out of scope for this decision but flagged for the implementation plan to address (typed target reference, e.g. an `ITargetable` marker or `int targetId`).

**TResult interaction (decision #1):** unaffected. Decision #1 already drops TResult; OnTrigger has no TResult either.

---

## Decision #4 — Port IDs are strings (field names)

### Problem

User-facing payload code today:
```csharp
[GraphEntry(FlowOutPortId = unchecked((int)0xCE00_0003u))]
public sealed class OnPlay : IGraphEntry<OnPlay>
{
    [GraphPort(Id = unchecked((int)0xCE00_1001u))]
    public int CardId;
}
```

Authors manually pick stable port IDs as 32-bit hex literals, write them with `unchecked((int)0x...u)` ceremony, and have to avoid collisions across the package by hand. No compile-time check. Hostile to read and edit. Built-in nodes carry the same noise (`Branch.TruePortId = unchecked((int)0x00000002u)` etc.).

### Why it happened

M2 designed stable port IDs as ints (compact, fast dict lookup) and required explicit authoring because no derivation rule existed. M2's `[GraphNode]` pipeline added FNV-hash IDs derived from type+field name (`EditorNodeIdentity`), but that derivation never extended to `[GraphEntry]` / `[GraphPort]` user payloads. The hex-literal authoring path stayed as the only option for entries / commands / event payloads.

### Where port IDs are used (audit)

- Hydration (`controller.Initialize`): Ports dict lookup by ID. **One-time per controller.**
- Bake time (GraphBaker): editor port names → port IDs via registry dicts. **One-time per asset import.**
- Asset serialization (ConnectionRecord, FlowEdge): persisted in the .asset. **One-time per save.**
- Flow walk (`flow.GoTo(portId)`): identifies which flow output to follow. **Per-step in the executor — but the int comparison happens against a small `flowEdges` list, not against any string.**
- Validation rules (EFG-V03): registry lookups by port name. **Edit-time only.**

**Not used at per-execution hot paths.** Once hydrated, `Condition.Read()` is direct field access on the typed `InputPort<bool>` — no dict lookup, no string compare. The plan's goal #7 "no string lookups" rule applies to per-execution code, not hydration.

### Options considered

**(a) FNV-derive int IDs from type-name + field-name; drop `[GraphPort(Id = ...)]` requirement.** Compact ints, derivation matches existing `[GraphNode]` pipeline. Refactor behavior: rename = ID changes = serialized graphs break (expected).

**(b) Sequential ints assigned at compile time.** Brittle on refactors (insert field shifts all later IDs).

**(c) Strings — use the field name as the port ID directly.** No derivation, no FNV, no hex. Field rename = port ID changes (same as (a) semantically). Asset YAML becomes readable (`fromPortId: "CardId"`). Compile-time collision check (duplicate field name = compile error). Zero authoring ceremony.

**(d) Keep current shape, just hide the `unchecked` ceremony.** Cosmetic only.

### Decision

**(c) — strings, field names as port IDs.**

### Rationale

- Plan's "no string lookups" rule constrains per-execution paths, not hydration. Audit confirms IDs are only used at hydration / bake / asset persistence — strings are fine.
- Same refactor semantics as FNV-derive ints (rename = breaks serialized graphs) — expected, no different from any other Unity field rename.
- Wins on every dimension *except* dict lookup speed at hydration, which is one-time and against a ~5-10-entry dict per node — negligible.
- Eliminates `[GraphPort(Id = ...)]`, `[GraphEntry(FlowOutPortId = ...)]`, `[GraphCommandPair].FlowInPortId/FlowOutPortId`, and the `unchecked((int)0x...u)` literal ceremony from all user code. Builtin nodes' int constants also collapse to either strings or `nameof(...)` references.
- Asset YAML becomes self-documenting (`fromPortId: "CardId"` instead of `fromPortId: 1326093079`).
- User's debuggability instinct is right.

### Consequences

**API surface changes:**
- `Flow.GoTo(int)` → `Flow.GoTo(string)`.
- `RuntimeNode.Ports` becomes `Dictionary<string, Port>`.
- `RuntimeNode.Bind(int dstPortId, RuntimeNode src, int srcPortId)` → `Bind(string dstPortName, RuntimeNode src, string srcPortName)`.
- `ConnectionRecord` and `FlowEdge` fields change from `int fromPortId / fromFlowPortId` etc. to `string fromPortName / fromFlowPortName`. Schema-version bump required for already-baked assets (or just rebake).
- `GraphPackageRegistry.NodeRegistration` port-id dicts: `Dictionary<string, int>` → `Dictionary<string, string>` (or just `HashSet<string>` since key + value are now redundant).
- Generator emit: registry blocks, port-id constants in built-in nodes, all simplify or disappear.

**Built-in nodes:**
- `Branch.TruePortId = "True"`, `FalsePortId = "False"`, `ConditionPortId = "Condition"` — could become `nameof()` references or just inline string literals.
- `[GraphPort]` attribute survives only as an opt-in override (e.g. when migrating an old serialized layout to a new field name without breaking existing assets — provide `[GraphPort(Name = "OldFieldName")]` so the runtime maps the old serialized name to the new field). Unlikely to need this in v1.

**Removed attributes:**
- `[GraphPort(Id = ...)]` — no longer required (and ID arg becomes meaningless).
- `[GraphEntry(FlowOutPortId = ...)]` — same; if the attribute survives, it's purely a marker.
- `[GraphCommandPair]` — already dropped per decision #2.

**Generator simplifications:**
- `EditorNodeIdentity.PortIdFor(...)` and equivalent FNV derivation helpers can be removed.
- `PayloadDiscovery.TryGetGraphPortId` becomes `PayloadDiscovery.GetPortName` (just returns the field name).
- `GraphRegistryEmitter.PortIdLiteral(...)` removed.

**Snapshot baselines:** all regenerate. Asset re-bake required for any existing `.gfmsmoke` / `.card` files (or migration script — probably easier to just re-author the smoke graph).

**Sandbox migration:**
- All `[GraphPort(Id = ...)]` and `[GraphEntry(FlowOutPortId = ...)]` annotations removed from M0 + CardSandbox payloads.
- Existing `.gfmsmoke` graph asset needs re-bake (or delete + re-author).

---

## Decision #5 — Two-tier RuntimeNode hierarchy: drop TRunner from built-in nodes

### Problem

Built-in framework primitives (`Branch`, `Cancel`, `Return`, `Not`) are currently parameterized by TRunner — `Branch<TRunner>`, `Cancel<TRunner>`, `Return<TRunner, TResult>` — even though their `Execute` bodies never reference `runner`. The TRunner is structural noise from inheriting `RuntimeNode<TRunner>`.

Consequences of the noise:
- Per-consumer closed-generic registrations: `MySmokeGraphRegistry` registers `Branch<MySmokeRunner>`, `CardSandboxGraphRegistry` registers `Branch<CardEffectRunner>`, etc. Same code emitted N times.
- Editor mirror is one `BranchEditorNode` per package — also duplicated.
- `Return<TRunner, TResult>` carries TWO type parameters when only TResult is semantically meaningful.
- The "Return doesn't appear in the picker" problem (original #4) cascades from this — multi-T closure expansion was deferred to M4.

### Why it happened

`RuntimeNode<TRunner>` was the only flow-bearing base in M2/M3. Built-in nodes inherited it because `Execute` had to take TRunner, even though they didn't use it. Nobody re-evaluated whether the framework primitives needed TRunner at all.

### Decision

**Two-tier RuntimeNode hierarchy. Both bases coexist; nodes pick the right one for their needs.**

```csharp
// Tier 1: runner-agnostic. Base for ALL nodes (data + flow). Built-in primitives live here.
public abstract class RuntimeNode
{
    public string nodeId;                        // string per decision #4
    public Dictionary<string, Port> Ports;
    public List<Connection> Connections;

    public virtual Task Execute(Flow flow) => Task.CompletedTask;  // default: stop
    public Connection Bind(string dstPortName, RuntimeNode src, string srcPortName) { ... }
}

// Tier 2: runner-typed. For nodes that need typed runner / scope access at Execute time.
public abstract class RuntimeNode<TRunner> : RuntimeNode where TRunner : GraphRunner
{
    TRunner? _runner;
    internal void BindRunner(TRunner runner) => _runner = runner;

    public sealed override Task Execute(Flow flow) => Execute(_runner!, flow);
    public abstract Task Execute(TRunner runner, Flow flow);
}
```

**Built-in nodes drop TRunner:**
```csharp
public sealed class Branch : RuntimeNode
{
    public InputPort<bool> Condition = null!;
    public override Task Execute(Flow flow) =>
        flow.GoTo(Condition.Read() ? "True" : "False");
}

public sealed class Cancel : RuntimeNode
{
    public override Task Execute(Flow flow) => flow.Cancel();
}

public sealed class Return<TResult> : RuntimeNode
{
    public InputPort<TResult> Value = null!;
    public override Task Execute(Flow flow) => flow.Return(Value.Read());
}

public sealed class Not : RuntimeNode { ... }   // already runner-agnostic
```

**Custom dispatchers stay runner-typed:**
```csharp
public sealed class DealDamageDispatcher : RuntimeNode<CardEffectRunner>
{
    public override Task Execute(CardEffectRunner runner, Flow flow)
    {
        var scope = (ICardEffectScope)flow.Scope!;
        // ... runner-typed and scope-typed access ...
    }
}
```

**`Flow` gains a runner reference (mirror of Scope):**
```csharp
public sealed class Flow
{
    public IEffectScope? Scope { get; internal set; }
    public GraphRunner? Runner { get; internal set; }   // NEW — set by controller at run start
    // ... existing members ...
}
```

The runner-typed override `Execute(TRunner runner, Flow flow)` reads `_runner` (bound at hydration), not `flow.Runner` directly — avoids per-Execute cast cost. `flow.Runner` exists for the rare case where a runner-agnostic node needs untyped access.

### Hydration

Controller.Initialize walks the asset's nodes:
- For each `RuntimeNode<TRunner>` instance (where TRunner matches the controller's runner type), call `BindRunner(_runner)` so the typed Execute dispatch works without per-Execute cast.
- For each `RuntimeNode` instance (no TRunner), nothing to bind.
- Type mismatch (e.g. someone serialized a `RuntimeNode<OtherRunner>` into a `GraphAsset<MyRunner>`) — throw at hydration with a clear error. Editor-side prevention is via the per-package registry: only nodes registered for the current package's runner appear in that package's picker (built-ins appear in every picker; runner-typed customs appear only in their own).

### Type safety boundaries

- **Asset level:** `GraphAsset<TRunner>` keeps TRunner so the importer + controller still type-check. `CardSandboxAsset` can't be loaded by a non-CardEffect controller.
- **Editor-picker level:** package registry filters which nodes are visible. Built-ins everywhere; runner-typed customs only in their package. Picker can never show `DealDamageDispatcher` in a non-CardEffect graph.
- **Hydration level:** type check at controller.Initialize as a safety net for hand-built or migrated assets.

### Rationale

- Direct answer to "why does Return need TRunner" — it doesn't, and neither do Branch/Cancel/Not.
- Built-in primitives become package-shipped non-generic concrete classes. Registered once per consumer (cross-asm walk) instead of closed N times. Editor mirror shipped once total.
- `Return<TResult>` parameterized only by what matters (the result type). Visual picker uses decision #3's dynamic-options pattern: one `ReturnEditorNode` with a `TypeReference` dropdown for TResult.
- Original problem #4 (no Return in picker) collapses into "Return is a runner-agnostic built-in, registered like Branch/Cancel; visual instantiation goes through dynamic-options".
- Custom dispatchers keep typed runner access via tier 2 — type safety where consumers benefit from it.
- Decision #1 simplifies further: `EntryRuntimeNode<TEntry>` (drop both TRunner and TResult) becomes the default for entries; `EntryRuntimeNode<TEntry, TRunner>` available for the rare entry that needs runner-typed access.
- `OnTrigger<TEvent>` (decision #3) extends `EntryRuntimeNode<OnTrigger<TEvent>>` — runner-agnostic, since it just routes the event into the flow.

### Consequences

**Type-system changes:**
- `RuntimeNode<TRunner>` keeps existing shape, gains `_runner` field + `BindRunner` + sealed Execute override.
- `RuntimeNode` (the existing data-only base) gains `virtual Task Execute(Flow flow)` — was previously execute-less.
- Built-in `Branch`, `Cancel`, `Return<TResult>`, `Not` reparented from `RuntimeNode<TRunner>` to `RuntimeNode`. ReturnBool already gone (M3).
- `EntryRuntimeNode<TEntry>` — drops TRunner. The 2-arg `EntryRuntimeNode<TEntry, TRunner>` survives as the runner-typed variant for the rare entry that needs it.

**Generator changes:**
- Discovery walk for `[GraphNode]` types no longer needs to close them at the consumer's runner — registers the open type directly when no TRunner generic.
- Per-package registry for built-ins becomes one registration per node type (not per `(node, runner)` pair).
- Editor mirror for built-in nodes shipped once in the package's editor asm; not re-emitted per consumer.
- `EmitGenericNodeArtifacts` simplifies for runner-agnostic nodes (no closure step).

**Runtime changes:**
- `Flow.Runner` added (set by `GraphExecutor.RunFlow` at run start, alongside Scope).
- Executor's flow walk loop unchanged — still calls `current.Execute(flow)`. RuntimeNode<TRunner>'s sealed override transparently dispatches to the typed body.

**Sandbox migration:**
- M0 sandbox tests' direct constructions: `new Branch<MySmokeRunner>()` → `new Branch()`. Same for Cancel, Return.
- `Return<MySmokeRunner, bool>` → `Return<bool>`.
- CardSandbox's `DealDamageDispatcher` stays `: RuntimeNode<CardEffectRunner>` (it needs typed runner access).

**Snapshot baselines:** all regenerate. Built-in node registrations collapse from per-runner to package-wide.

**Resolves original problem #4** (no Return in picker) — Return is now a runner-agnostic built-in like Branch; the dynamic-options editor (#3) handles the TResult dropdown.

---

## Decision #6 — CardSandbox sample hygiene (problems #5 + #6)

### Problem

**#5 — `Strike500.cs` is 97 lines wrapped in `static class Strike500 { ... }`** with nested `OnPlayEntry`, nested `DealDamageDispatcher`, a `BuildAsset()` factory, plus a separate `DealDamageCommand` with a 13-line docstring. The static-class-as-namespace pattern is confused — `Strike500` isn't a runtime entity, marker, or instance; it's a folder organizer pretending to be a class.

**#6 — `DealDamageCommand` docstring is as long as the class itself**, explaining `[GraphCommandPair]`, `[GraphPort]`, and generator emission rules.

### Why it happened

**#5:** Phase 4 brief explicitly said "Don't delete the hand-authored Strike500.OnPlayEntry / DealDamageDispatcher" — that was wrong once CardSandbox got `[GraphPackage]` (the post-M3 step). Generator emits equivalents; the hand-authored ones are dead weight. Static-class-as-namespace was the agent's coping mechanism for organizing the dead code.

**#6:** Agent over-documented `[GraphCommandPair]` because the attribute itself was the explanation surface — explaining-the-explanation pattern.

### Why most of this auto-resolves from prior decisions

- **Decision #2** drops `[GraphCommandPair]` → DealDamageCommand becomes `public sealed record DealDamageCommand : Command<Unit> { public int Amount; ... }`. Docstring shrinks to one line naturally.
- **Decision #4** drops `[GraphPort(Id = ...)]` → port-id ceremony gone.
- **Decision #3** introduces `OnTrigger<TEvent>` primitive → PlusOneDamage's hand-authored trigger entry becomes dead code (graph uses `OnTrigger<DamageDealt>` directly).
- **Decision #5** + CardSandbox-authorable's generator emit → `Strike500.OnPlayEntry` and `Strike500.DealDamageDispatcher` are dead code.

After all the above, only `BuildAsset()` factories and the bare command/event/entry-marker classes remain as hand-written code.

### Decision

**One type per file. No static-class-as-namespace wrappers. Flat folder layout that names what's inside.**

```
Samples/CardSandbox/Runtime/
├── CardEffectRunner.cs          // unchanged
├── CardEffectGraphAsset.cs      // unchanged
├── CardCommandDispatcher.cs     // unchanged (Mode 2 dispatcher base)
├── EventBus.cs                  // unchanged (sample-only)
├── IEffectScope.cs              // unchanged (CardEffectScope + DamageSink + sample marker)
├── Events/
│   └── DamageDealt.cs           // [GraphEvent] public sealed class DamageDealt { public int Amount; }
├── Commands/
│   └── DealDamageCommand.cs     // public sealed record DealDamageCommand : Command<Unit> { public int Amount; }
├── Entries/
│   └── OnPlay.cs                // public sealed class OnPlay : IGraphEntry { }
└── Cards/
    ├── Strike500.cs             // public static class Strike500 { public static CardEffectGraphAsset BuildAsset() { ... } }
    └── PlusOneDamage.cs         // public static class PlusOneDamage { public static CardEffectGraphAsset BuildAsset() { ... } }
```

The `Cards/` folder still uses `static class Strike500 { ... BuildAsset() ... }` because the factory needs *some* container — but the static class now contains exactly one method, so it's a true namespace use, not a fake class wrapper around a tangled web of types.

If implementation lands `.card` asset files for visual authoring, they live alongside the BuildAsset factories: `Cards/Strike500.card` next to `Cards/Strike500.cs`.

### Docstring discipline

- One-line summary per type. No multi-paragraph explanations unless the WHY is genuinely non-obvious (matches the project's working agreement).
- Don't explain framework attributes — the framework documents itself.
- Don't explain implementation choices — that's commit-message territory.

### Rationale

- Most of the cleanup is automatic from prior decisions; this decision codifies the file-layout intent so the next agent doesn't re-create the same nested mess.
- One type per file is easy to enforce in review.
- Sample's purpose stays visible at the folder level: data classes (Events/), command types (Commands/), entry markers (Entries/), card build factories (Cards/).

### Consequences

- Strike500.cs shrinks from 97 lines to ~25 (just BuildAsset + brief summary).
- DealDamageCommand.cs becomes ~15 lines.
- New files: `Events/DamageDealt.cs`, `Entries/OnPlay.cs`.
- DealDamageDispatcher and OnPlayEntry classes deleted (dead code).
- PreDamageDealtEvent + DamageDealtEvent collapse to one `DamageDealt` class (per decision #3 — phase becomes a Timing dropdown on OnTrigger).
- `[GraphEvent]` attribute applied to `DamageDealt` (per decision #3) so it appears in the OnTrigger dropdown.
- Tests still call `Strike500.BuildAsset()` — the factory is the only public surface in Strike500.cs.

---

## Decision #7 — Problem #9: NaughtyAttributes inspector noise on GraphObject

### Problem

NaughtyAttributes' global inspector override fires "The target object is null. Check for missing scripts." log errors when selecting `.gfmsmoke` / `.card` files in the Project view. Pure inspector noise — graph editor works, runtime unaffected — but visually alarming.

The earlier `GraphAssetEditor` fix (commit `80fcfee`) covered our `GraphAsset<>` sub-asset. The error fires on the *main* asset, which is `Unity.GraphToolkit.Editor.Implementation.GraphObjectImp` — internal to GraphToolkit, can't be targeted by `[CustomEditor]` from outside their package.

### Why it happened

- NaughtyAttributes registers `[CustomEditor(typeof(UnityEngine.Object), true)]` — claims the inspector for everything by default.
- `GraphObject` (the actual main asset class) is `internal` in GraphToolkit; not visible from our package.
- GraphToolkit ships no `[CustomEditor]` of its own for GraphObject (verified — no CustomEditor anywhere in `com.unity.graphtoolkit`).
- NaughtyAttributes' `ReflectionUtility.GetAllFields(target, ...)` logs an error when `target == null` instead of yielding empty. Triggers on `[SerializeReference]` polymorphic data with null-or-internal-typed entries (which GraphObject has).

### Options

- **(a) Live with it.** Log noise, no functional impact. Zero work.
- **(b) Fork NaughtyAttributes** to add the null-guard. Maintenance burden on every upgrade.
- **(c) File upstream + bridge with (b).** Slow; (a) bridges anyway.
- **(d) Override at `[CustomEditor(typeof(ScriptableObject), true)]`.** Destructive overreach — would also break user's other custom editors.
- **(e) Remove NaughtyAttributes from the project** if no actual usage exists. Cleanest if viable.

### Decision

**(a) — live with it.**

### Rationale

- NaughtyAttributes' `OnInspectorGUI` is third-party code. The bug is theirs. We can't fix it from inside our package without overriding ScriptableObject globally (option d, which is harmful).
- The errors are log warnings only — graph authoring works, runtime is unaffected, no observable feature regression.
- Forking + maintaining a patched NaughtyAttributes (option b) costs more than the noise costs.
- Removing NaughtyAttributes (option e) is potentially viable but requires a project-wide audit of `[Button]` / `[ShowIf]` / etc. usage — out of scope for the GraphFlow follow-ups.

### Consequences

- Documented as a known papercut in this decisions file.
- Implementation plan will not address #9 — left as-is.
- If the noise becomes unbearable later, escalate to (e) (remove NaughtyAttributes if unused) or (b) (fork + patch).

