# Post-M3 Implementation Plan

Companion to [PostM3-FollowUps.md](PostM3-FollowUps.md), which captures the seven decisions agreed with the user. This document sequences those decisions into build-green phases.

## Status: Done (all six phases landed)

| Phase | Decisions | Commit |
| --- | --- | --- |
| 1 | #1 + partial #3 (markers) + reflection drop | `9b42a63` |
| 2 | #4 (string port IDs) + #5 (two-tier RuntimeNode) | `edfefa5` |
| 3 | #3 (OnTrigger + dynamic-options editor) | `4543dd8` |
| 4 | #2 (drop `[GraphCommandPair]`) | `7eacac6` |
| 5 | #6 (CardSandbox hygiene + dead code) | `a6a95ea` |
| 6 | Plan-doc updates | this commit |

Verified at every phase: all 7 GraphFlow tests pass (5 M0 + 2 CardSandbox), snapshot harness 14/14 stable, Unity batchmode compile zero `error CS` lines.

Decision #7 (NaughtyAttributes inspector noise) is "live with it" — no code change.

## Constraints

- **Build green at every commit.** Each commit must end at: `dotnet build Generators/...` exit 0, snapshot harness `dotnet run` exit 0, Unity batchmode compile zero `error CS####` lines, all M0 + CardSandbox tests passing.
- **One feature per phase.** Phases sequence to minimize cross-phase rebakes, but each phase is internally atomic.
- **Snapshot baselines regenerate inside the phase that breaks them**, so a phase always commits with passing snapshots.
- **Existing graph assets re-bake or get re-authored as part of the phase that changes the asset schema** (only phase 2 — string port IDs).

## Phase ordering rationale

Touched-files matrix per decision:

| | Concepts.cs | EntryRuntimeNode | GraphController | RuntimeNode hierarchy | Generator emit | Asset schema | Sample code |
|---|---|---|---|---|---|---|---|
| #1 drop TResult | ✓ | ✓ | ✓ | | ✓ | | ✓ |
| #2 drop `[GraphCommandPair]` | | | | | ✓ | | ✓ |
| #3 IGraphEntry + OnTrigger | ✓ | | | | ✓ | | ✓ |
| #4 string port IDs | | | | ✓ | ✓ | ✓ | ✓ |
| #5 two-tier RuntimeNode | | ✓ | ✓ | ✓ | ✓ | | ✓ |
| #6 sample hygiene | | | | | | | ✓ |
| #7 NaughtyAttributes | — | — | — | — | — | — | — |

#1 and #3 overlap heavily on Concepts.cs + entry runtime + generator entry-emit. Land them together so the generator emit pivot is one operation, not two.

#4 is the only schema change — asset re-bake happens once.

#5 has the largest blast radius (every built-in node, every registry registration, every executor signature). Land it after #1+#3 stabilize the entry side, before #2/#6 cleanup.

## Phase 1 — Marker simplification + drop TResult + drop reflection

**Decisions:** #1 (TResult), #3 (markers — runtime side only, OnTrigger primitive deferred to phase 3), partial #5 (EntryRuntimeNode shape).

**Goal.** Collapse entry markers to non-generic `IGraphEntry`, drop TResult from entries, collapse `Run<TEntry, TResult>` + `RunFlow<TEntry>` into one `Run<TEntry>(payload) → Task<Flow>`, eliminate reflection from GraphController via generator-emitted bridges.

**What lands:**
- `Concepts.cs`: replace `IGraphEntry<TPayload, TResult>` / `IGraphEntry<TPayload>` / `IGraphTrigger<TEvent>` with `IGraphEntry` (non-generic). `IGraphAction<TRunner>` + `IExecutable<TRunner>` unchanged. `Unit` struct removed (no longer needed). `Timing` enum + `[GraphEvent]` attribute deferred to phase 3 (when OnTrigger lands).
- `EntryRuntimeNode<TEntry, TRunner, TResult>` → `EntryRuntimeNode<TEntry, TRunner>` (TResult dropped; TRunner stays for now — full drop happens in phase 2 alongside the RuntimeNode hierarchy reshuffle). `BindRunner` becomes `Func<TEntry, Task<Flow>>` (returns the Flow that ran, not a typed result).
- `GraphController.Run<TEntry, TResult>` deleted. `RunFlow<TEntry>` renamed to `Run<TEntry>(payload, ct = default) → Task<Flow>` — the single Run API. `EntryNodes` catalog unchanged.
- Generator-emitted entry bridge per package: `<Stem>EntryBridges.g.cs` with one per-payload-type `IEntryBridge` implementation that knows how to find its entry node, set payload, run, return Flow. Controller uses `Dictionary<Type, IEntryBridge>` populated at Initialize (no reflection).
- `EmitEntryRuntime` drops the TResult arg + return-Flow dispatch shape.
- Payload discovery: walk package asm for `IGraphEntry`-implementing types. Self-referential `IGraphEntry<self>` and `IGraphTrigger<self>` no longer required.
- M0 sandbox payloads migrate: `OnPlay : IGraphEntry<OnPlay>` → `OnPlay : IGraphEntry`. CardSandbox payloads same.
- M0SmokeRuntimeTests + Strike500Tests migrate: `controller.RunFlow(...)` → `controller.Run(...)` (signature unchanged from caller's POV — Task<Flow>). `await controller.Run<X, Y>(payload)` calls (if any — none currently) gone.

**What does NOT land:**
- `OnTrigger<TEvent>` primitive (phase 3 — needs the dynamic-options editor + RuntimeNode reshuffle).
- TRunner stays on EntryRuntimeNode and built-in nodes (phase 2).
- String port IDs (phase 2).
- `[GraphCommandPair]` (phase 4).
- Sample structural cleanup (phase 5 — let phase 3's OnTrigger pivot drive most of the deletions).

**Files touched:**
- `Assets/Packages/com.scaffold.graphflow/Runtime/Concepts.cs`
- `Assets/Packages/com.scaffold.graphflow/Runtime/EntryRuntimeNode.cs`
- `Assets/Packages/com.scaffold.graphflow/Runtime/GraphController.cs`
- `Generators/Scaffold.GraphFlow.PackageGenerator/GraphPayloadNodeEmitter.cs` (entry emit + new bridge emit)
- `Generators/Scaffold.GraphFlow.PackageGenerator/GraphPackageTrioEmitter.cs` (registry includes bridges)
- `Generators/Scaffold.GraphFlow.PackageGenerator/PayloadDiscovery.cs` (IGraphEntry walk)
- `Assets/Packages/com.scaffold.graphflow/Samples/M0Sandbox/Runtime/Smoke/Payloads.cs`
- `Assets/Packages/com.scaffold.graphflow/Samples/M0Sandbox/Tests/M0SmokeRuntimeTests.cs`
- `Assets/Packages/com.scaffold.graphflow/Samples/CardSandbox/Runtime/Events.cs`
- `Assets/Packages/com.scaffold.graphflow/Samples/CardSandbox/Runtime/Cards/*.cs`
- `Assets/Packages/com.scaffold.graphflow/Samples/CardSandbox/Tests/Strike500Tests.cs`
- Snapshot baselines regen.

**Owner:** agent (mechanical + design-bearing on the bridge emit shape).

**Validation gate:** dotnet build + snapshot harness clean + Unity batchmode compile zero error CS + all 7 tests pass.

**Risks:**
- Bridge emit shape needs care to keep `IEntryBridge` interface non-generic so the controller can hold them in a `Dictionary<Type, IEntryBridge>` without reflection. Bridge implementation closes over TEntry.
- Sandbox tests heavily reference `RunFlow` vs `Run` — careful sed.

---

## Phase 2 — String port IDs + two-tier RuntimeNode hierarchy

**Decisions:** #4 (port IDs), #5 (two-tier hierarchy), finishing #1 (drop TRunner from EntryRuntimeNode).

**Goal.** Strings as port IDs end-to-end (asset schema, runtime, registry, generator). Built-in nodes drop TRunner and become package-shipped non-generic concrete types. RuntimeNode<TRunner> survives for typed dispatchers. Asset re-bake required.

**What lands:**
- `RuntimeNode` (non-generic base) gains `virtual Task Execute(Flow flow) => Task.CompletedTask`.
- `RuntimeNode<TRunner>` keeps existing shape; gains `_runner` field + `BindRunner(TRunner)` + sealed override `Execute(Flow flow) => Execute(_runner!, flow)`.
- Built-in `Branch`, `Cancel`, `Return<TResult>`, `Not` reparent from `RuntimeNode<TRunner>` to `RuntimeNode`. Bodies use `Execute(Flow flow)` directly.
- `Return<TRunner, TResult>` → `Return<TResult>`.
- `EntryRuntimeNode<TEntry, TRunner>` → `EntryRuntimeNode<TEntry>` (drops TRunner — uses `flow.Runner`-based access if needed, but most entries don't need typed runner). Two-arg `EntryRuntimeNode<TEntry, TRunner>` survives as the runner-typed variant for the rare entry that does (custom dispatch entries).
- `Flow` gains `public GraphRunner? Runner { get; internal set; }`.
- `GraphExecutor.RunFlow` sets `flow.Runner = runner` at run start. Walks `RuntimeNode? current` and calls `current.Execute(flow)` (virtual dispatch handles tier-2's typed override).
- `GraphController.Initialize` calls `BindRunner(_runner)` on every `RuntimeNode<TRunner>` instance in the asset. Throws on type mismatch.
- **Asset schema change:** `ConnectionRecord.fromPortId/toPortId` change from `int` to `string`. Same for `FlowEdge.fromFlowPortId/toFlowPortId`. `EntryIndex` unchanged. Bump `GraphAsset.schemaVersion` to 2.
- `RuntimeNode.Ports` becomes `Dictionary<string, Port>`.
- `RuntimeNode.Bind(int dstPortId, ...)` → `Bind(string dstPortName, ...)`.
- `Flow.GoTo(int)` → `Flow.GoTo(string)`.
- `GraphPackageRegistry.NodeRegistration` port-id dicts: `Dictionary<string, int>` → `HashSet<string>` (or kept as `Dictionary<string, string>` with name→name identity if more readable).
- `[GraphPort(Id = ...)]` and `[GraphEntry(FlowOutPortId = ...)]` ID args removed (attributes survive only as markers if anywhere). `unchecked((int)0x...u)` ceremony deleted everywhere.
- Built-in node port-id constants: `public const int TruePortId = 0x...` → `public const string TruePortId = "True"` (or use `nameof()` / inline strings).
- Generator: registry block emit, port-id constants, base-class closures all simplified. Built-in registrations no longer close per-runner — one Branch registration per package, ever.
- Cross-asm walk for `[GraphNode]` types: still walks all asms referencing `Scaffold.GraphFlow`, but emits non-runner-closed registrations for runner-agnostic nodes. Runner-typed nodes (rare) close to the consumer's runner as before.
- Editor mirror for built-in nodes ships hand-written in `Assets/Packages/com.scaffold.graphflow/Editor/Nodes/` once. No per-consumer emission.
- `GraphBaker` writes string port names instead of int IDs.
- M0 + CardSandbox payloads: drop `[GraphPort(Id = ...)]` everywhere. Drop `[GraphEntry(FlowOutPortId = ...)]` if it survives only as a marker, leave it; if its only purpose was the ID, remove the attribute.
- `.gfmsmoke` graph re-baked (delete + re-author, OR write a one-shot migration that converts int IDs to string names — implementer's call; re-authoring is probably cheaper).
- Snapshot baselines regenerate.

**What does NOT land:**
- `OnTrigger<TEvent>` (phase 3).
- `[GraphCommandPair]` removal (phase 4).
- Sample restructure (phase 5).

**Files touched:**
- Most of `Assets/Packages/com.scaffold.graphflow/Runtime/`.
- Most of `Generators/Scaffold.GraphFlow.PackageGenerator/`.
- `Assets/Packages/com.scaffold.graphflow/Editor/` (new built-in editor mirrors).
- All `[GraphPort]`, `[GraphEntry]` annotated payloads in samples.
- Asset re-bake of `GraphFlow M0 Smoke Graph.gfmsmoke` (and any test `.card` file).
- Snapshot baselines.

**Owner:** agent (largest mechanical scope; design-bearing on the typed-runner BindRunner caching + virtual dispatch shape).

**Validation gate:** triple gate, plus an explicit "asset re-bakes cleanly" check (open `.gfmsmoke` in Unity, save, verify the bake produces a sub-asset with string port IDs).

**Risks:**
- Asset migration: existing `.gfmsmoke` won't open because its serialized int port IDs no longer match the runtime's string-keyed dict. Either delete + re-author, OR write a one-shot import migration. Re-authoring is the simpler call for v1.
- Built-in editor mirror move from generator-emitted to hand-written: need to make sure cross-asm registry walks find the hand-written ones via `[GraphNode]` discovery on the runtime side.
- Snapshot harness fixture: M0 baselines all regenerate; verify the regen produces sensible diffs (not random reordering).

---

## Phase 3 — `OnTrigger<TEvent>` primitive + dynamic-options editor

**Decisions:** #3 (OnTrigger + Timing + [GraphEvent] + dynamic-options editor).

**Goal.** Single OnTrigger primitive with a TypeReference dropdown for the event type and a Timing dropdown for Before/After. Per-event-type trigger entry classes (PreDamageDealtEvent, DamageDealtEvent) collapse to one event class (DamageDealt). Dynamic-port emission via GraphToolkit's `OnDefineOptions` + `OnDefinePorts` reading option values.

**What lands:**
- New types in package runtime:
  - `Timing` enum (Before, After).
  - `[GraphEvent]` attribute (in AttributesLib) — opts an event class into the OnTrigger picker.
  - `OnTrigger<TEvent> : EntryRuntimeNode<OnTrigger<TEvent>>` — one built-in entry primitive. Carries `Event` payload reference + `Timing` field.
- New editor mirror (hand-written in package Editor) — `OnTriggerEditorNode : Node` — single non-generic mirror that uses `OnDefineOptions` for the TypeReference + Timing dropdowns and `OnDefinePorts` to dynamically emit output ports for the picked event's `[GraphPort]`-tagged fields.
- Generator: new `<Stem>GraphRegistry.EventTypes` table — list of `(Type, IReadOnlyList<PortFieldMeta>)` entries built from `[GraphEvent]` walk of the package asm. Editor mirror reads from this at edit time to populate the dropdown + define dynamic ports.
- Hydration constructs `OnTrigger<T>` reflectively (one-time at hydration, not hot path) from the editor node's serialized event-type option.
- TypeReference integration: depends on Scaffold.Types' `TypeReference` shape — pick a serializable form GraphToolkit options can persist.
- CardSandbox migration: `PreDamageDealtEvent` + `DamageDealtEvent` collapse to one `[GraphEvent] DamageDealt` class. Hand-authored trigger entry types deleted (OnTrigger primitive replaces them). Strike500.BuildAsset() builds a graph with `OnTrigger<DamageDealt>` + Timing.Before for PlusOneDamage's effect.
- Sample's bus shape: collapse PreDamageDealt + DamageDealt into one DamageDealt event published twice with Timing semantics. Implementation in CardSandbox's EventBus + DealDamageCommand.
- Snapshot baselines regen — per-event mirrors disappear, one OnTriggerEditorNode replaces them.

**What does NOT land:**
- `[GraphCommandPair]` removal (phase 4).
- Sample restructure (phase 5).

**Files touched:**
- `Generators/Scaffold.GraphFlow.Attributes/GraphEventAttribute.cs` (new)
- `Assets/Packages/com.scaffold.graphflow/Runtime/Timing.cs` (new)
- `Assets/Packages/com.scaffold.graphflow/Runtime/OnTrigger.cs` (new)
- `Assets/Packages/com.scaffold.graphflow/Editor/Nodes/OnTriggerEditorNode.cs` (new)
- Generator: trio emitter (`<Stem>GraphRegistry.EventTypes` table emission), payload discovery (find `[GraphEvent]` types), editor-emit (skip per-event mirrors).
- `Assets/Packages/com.scaffold.graphflow/Samples/CardSandbox/Runtime/Events.cs` (collapse to single DamageDealt + `[GraphEvent]`).
- `Assets/Packages/com.scaffold.graphflow/Samples/CardSandbox/Runtime/EventBus.cs` (Timing-aware publish/subscribe).
- `Assets/Packages/com.scaffold.graphflow/Samples/CardSandbox/Runtime/Commands/DealDamageCommand.cs` (publish DamageDealt twice with Timing.Before / Timing.After).
- `Assets/Packages/com.scaffold.graphflow/Samples/CardSandbox/Runtime/Cards/PlusOneDamage.cs` (BuildAsset uses OnTrigger<DamageDealt>).
- `Assets/Packages/com.scaffold.graphflow/Samples/CardSandbox/Tests/Strike500Tests.cs` (subscription wiring uses OnTrigger pattern).
- Snapshot baselines.

**Owner:** agent (design-bearing on the dynamic-options editor implementation; needs careful testing of the TypeReference dropdown).

**Validation gate:** triple gate, plus explicit verification that the OnTrigger picker dropdown lists `[GraphEvent]`-tagged types and the editor's port shape changes when the dropdown changes.

**Risks:**
- TypeReference + GraphToolkit's IOptionDefinitionContext.AddOption integration: needs verification that GraphToolkit serializes TypeReference correctly and exposes it back via GetNodeOptionByName.
- Dynamic port emission: GraphToolkit re-runs OnDefinePorts when options change, but hydration needs to know what ports to expect. Verify the editor regenerates port definitions on option change AND the asset persists the resulting wires through option changes (probably wires reset when port shape changes — acceptable for v1).
- Reflection at hydration: OnTrigger<T> is closed at hydration time via reflection. One-time per controller, not hot path. Acceptable per decision #3.

---

## Phase 4 — Drop `[GraphCommandPair]`

**Decision:** #2.

**Goal.** Generator discovers Mode-2 commands by walking the package asm for `Command<TResult>` subclasses; reads result type from the closed base directly; no attribute required.

**What lands:**
- `GraphCommandPairAttribute` removed from AttributesLib.
- Payload discovery: instead of `TryReadCommandPairAttribute`, walk the package asm for any class assignable to `[GraphPackage].CommandBase` closed-or-open, read TResult from the closed base via Roslyn.
- `FindResultTypeFromDispatcherSubclass` becomes the only path (no longer fallback).
- Generator cross-asm `typeof()` quirk handling: still in place because the underlying compiler issue exists, but it's now the primary discovery mechanism, not a fallback.
- CardSandbox's `DealDamageCommand` simplifies to `public sealed record DealDamageCommand : Command<Unit> { public int Amount; ... }` — no attribute.
- Snapshot baselines unchanged for M0 (M0 doesn't use Mode 2). CardSandbox baselines regen if they exist.

**Files touched:**
- `Generators/Scaffold.GraphFlow.Attributes/GraphCommandPairAttribute.cs` (delete).
- `Generators/Scaffold.GraphFlow.PackageGenerator/PayloadDiscovery.cs` (rework command discovery).
- `Generators/Scaffold.GraphFlow.PackageGenerator/GraphPayloadNodeEmitter.cs` (drop attribute reading paths).
- `Assets/Packages/com.scaffold.graphflow/Samples/CardSandbox/Runtime/Commands/DealDamageCommand.cs`.

**Owner:** me (small enough to do inline).

**Validation gate:** triple gate.

**Risks:** none significant — the attribute is genuinely redundant; discovery rework is a small sed.

---

## Phase 5 — Sample hygiene + dead code removal

**Decision:** #6, plus tidy-up of anything left over from earlier phases.

**Goal.** CardSandbox file layout per decision #6. Hand-authored OnPlayEntry / DealDamageDispatcher deleted (already dead from earlier phases — this is the explicit sweep). Docstring discipline applied.

**What lands:**
- Restructure `Assets/Packages/com.scaffold.graphflow/Samples/CardSandbox/Runtime/`:
  - Move per decision #6's tree (Events/, Commands/, Entries/, Cards/).
  - Delete dead types: `Strike500.OnPlayEntry`, `Strike500.DealDamageDispatcher`, `PlusOneDamage` hand-authored trigger.
  - One type per file.
- Trim docstrings to one-line summaries; delete redundant explanations of framework attributes.
- Verify both `BuildAsset()` factories still work after the type relocations.
- Tests still pass.

**Files touched:**
- All under `Assets/Packages/com.scaffold.graphflow/Samples/CardSandbox/Runtime/`.

**Owner:** me (mechanical sweep; small scope).

**Validation gate:** triple gate.

**Risks:** none — purely a code-organization commit.

---

## Phase 6 — Update plan docs

**Goal.** Mark the seven decisions implemented in `PostM3-FollowUps.md`. Cross-link to commits. Decide whether to fold this into `ExecPlan-v2.md` as a "PostM3 Polish" closeout subsection mirroring M1/M2/M3 closeout style.

**What lands:**
- `Plans/GraphFlow/PostM3-FollowUps.md` gains a "Status" section per decision marking each as implemented + commit reference.
- Optional: `Plans/GraphFlow/ExecPlan-v2.md` gains a "PostM3 Polish closeout" subsection summarizing the seven decisions and their commits.

**Owner:** me (doc work, full conversational context preferred).

**Validation gate:** none (docs only).

---

## Cross-phase invariants

- After phase 1: `controller.Run<TEntry>(payload) → Task<Flow>` is the only Run API. No reflection in controller.
- After phase 2: port IDs are strings everywhere. Built-in nodes are non-generic. RuntimeNode<TRunner> survives only for typed dispatchers.
- After phase 3: triggers are authored visually as `OnTrigger<T>` with Timing dropdown; no per-event-class trigger entries.
- After phase 4: Mode-2 commands need no attribute decoration.
- After phase 5: CardSandbox is one-type-per-file with no static-class-as-namespace wrappers.

## Out of scope (intentionally deferred)

- M4 polish items from the plan (remaining catalog, multi-T `[GraphNode]`, `Connection` type-conversion, editor-visual API pass — the visual API pass partially overlaps with phase 3's dynamic-options work but isn't the same scope).
- Extraction of `M0SmokeRuntimeTests` into the package's own Tests/ folder (the M3 deferral noted in M3 closeout).
- NaughtyAttributes inspector noise on .gfmsmoke / .card selection (decision #7 = live with it).

## Estimated sequencing

- Phase 1: 1 day, agent.
- Phase 2: 1.5–2 days, agent. Largest scope.
- Phase 3: 1–1.5 days, agent. Dynamic-options editor is the unknown.
- Phase 4: 1–2 hours, inline.
- Phase 5: 1–2 hours, inline.
- Phase 6: 30 minutes, inline.

Total: ~4–5 days of focused work for the seven decisions.
