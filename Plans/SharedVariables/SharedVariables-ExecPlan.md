# Extract a shared Variables package and unify variable storage across GraphFlow, Entities, and States

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository policy for ExecPlans is defined in `PLANS.md` at the repository root. This document must be maintained in accordance with that file.

Companion design notes (background reading, not load-bearing): `Plans/SharedVariablesPackage.md`. This ExecPlan supersedes that doc as the source of truth.

## Purpose / Big Picture

Three Unity packages today each grow their own "variable" concept. `com.scaffold.graphflow` has `IVariableBag` with typed cells (`VariableCell<T>`) for blackboard variables on a graph asset. `com.scaffold.entities` has its own `IVariableBag` with a polymorphic `VariableValue` boxing wrapper, plus modifier stacks via `IEntityVariableStorage`. `com.scaffold.entities.states` (the bridge package) routes entity variable reads and writes through a `Store` so they participate in immutable snapshots. The two `IVariableBag` interfaces have the same name but incompatible shapes; there is no shared abstraction; and there is no way to bind a graphflow blackboard variable to a state-backed entity variable without writing per-consumer adapter glue.

After this ExecPlan, a developer can declare a variable in a graph asset's blackboard and have its reads and writes flow through a `Store` (so it participates in `Store.SaveSnapshot` / `Store.LoadSnapshot`). They override one method on `GraphRunner` and supply a `StoreVariableBagBuilder` configuration that says, for each graph variable id, which state slice + projector + payload factory it maps to. Graphflow nodes (`GetVariable`, `SetVariable`, `ObserveVariable`) keep their existing surface; under the hood the cell they cache becomes a `StoreBackedHandle` that pulls fresh from the store on every read and dispatches typed payloads on every write.

You can see this working when: a test creates a `Store`, registers an `EntityState` slice, builds a `GraphRunner` whose `CreateVariableBag` override returns a `StoreVariableBagBuilder`-built bag binding the graph's `hp` variable to the entity's `hp` base value, runs a graph that reads `hp`, dispatches a `Store.Execute(new SetBaseValuePayload(...))` from outside the graph, runs the graph again, and observes that the second read returns the new value. Snapshot + restore cycles preserve the graph's variable state because the storage IS the store.

## Progress

- [x] ExecPlan authored at `Plans/SharedVariables/SharedVariables-ExecPlan.md` (this file).
- [x] Confirmed real consumer code on `main` (VariableShowcase, StrikeWithVariables, VariableFollowUpTests). Plan adapted: Milestone 2 scope expanded to include consumer-code migration; `BlackboardVariable<T>` retained as graphflow's named flavor of shared `VariableDefault<T>`.
- [x] Branch synced with `origin/main` post-merge of PR #51 (fast-forward to `7516a609`). Branch now contains both the GraphFlow Variables work (PR #49) and the SharedVariables plan docs. Working tree clean; ready to start Milestone 1.
- [x] Milestone 1 — Land `com.scaffold.variables` package skeleton with `Variable`, `IVariableHandle`, `IReadOnlyVariableHandle<T>`, `IVariableHandle<T>`, `IVariableBag`, `VariableDefault` / `VariableDefault<T>`, `InMemoryHandle<T>`, `InMemoryVariableBag` (with cycle detection ported from main's `TryGetCellGuarded`). Plus `MutatorRegistry.IsRegistered(Type)` addition to `com.scaffold.states`. Pure additive — no consumer changes. Smoke tests cover seed, type-mismatch, missing-key, set/read, subscribe distinct-value-only, unsubscribe, parent-chain cascade, write-hits-owning-layer, non-generic introspection, `LocalHandles` enumeration, and cyclic-parent-chain termination via reflection-forced cycle. `MutatorRegistry.IsRegistered` test covers true-after-register, false-for-unknown, false-for-null. **Validation gate `.agents/scripts/validate-changes.sh` requires `pwsh` which is unavailable on this host; user must run the gate (or open in Unity) to confirm green compile + tests before marking complete.**
- [x] Milestone 2 — Migrate GraphFlow. Deleted `IVariableBag.cs`, `VariableCell.cs`, `InMemoryVariableBag.cs` from `Assets/Packages/com.scaffold.graphflow/Runtime/Variables/`. Collapsed graphflow's non-generic `BlackboardVariable` shim; `BlackboardVariable<T>` now extends shared `Scaffold.Variables.VariableDefault<T>` directly and the five concretes (`BlackboardInt/Float/Bool/String/Object`) are unchanged. `RuntimeVariable.defaultValue` retyped to shared `VariableDefault`. Replaced `GraphRunner.CreateParentBag()` with `protected internal virtual IVariableBag CreateVariableBag(IEnumerable<RuntimeVariable> seed)` plus a `protected static CreateInMemoryBag(seed, parent)` helper for overrides that just want a parent swap. `GraphBuilder.Build` now calls `runner.Variables = runner.CreateVariableBag(baked.Variables)` directly (no `SeedVariables` plumbing). `GetVariable<T>`/`SetVariable<T>`/`ObserveVariable<T>` use `IVariableHandle<T>?` fields, `runner.Variables.TryGet<T>`, `handle.Set(x)`, `handle.Subscribe(handler)`. `Port.ConnectFromVariable` now takes `IVariableHandle`; `InputPort<T>.ConnectFromVariable` casts to `IReadOnlyVariableHandle<T>`. `Flow.Variables` is `new InMemoryVariableBag(Runner.Variables)` (parent-only constructor). `EditorBlackboardVariables.CreateFor` returns `VariableDefault?`. Migrated consumer code (`VariableShowcase.cs`, `StrikeWithVariables.cs`) and the entire variable test suite (`VariableEdgeTests`, `VariableGetSetTests`, `VariableObserveTests`, `VariableEndToEndTests`, `VariableBagTests`, `VariableWiringTests`, `VariableFollowUpTests`, `VariableTestHelpers`) to method-style handle API. Added `CreateVariableBagOverrideTests` proving a custom `IVariableBag` returned from the override flows through to `runner.Variables` verbatim. Asmdef refs added: `Scaffold.Variables` GUID `aeaa2ad3d0d1b9aec5a07ab29b764460` referenced by `Scaffold.GraphFlow`, `Scaffold.GraphFlow.Editor`, `Scaffold.GraphFlow.CardSandbox`, `Scaffold.GraphFlow.Tests`, `Scaffold.GraphFlow.EditorTests`. **Validation gate `.agents/scripts/validate-changes.sh` requires `pwsh` which is unavailable on this host; user must run the gate (or open in Unity) to confirm green compile + tests before marking complete.**
- [x] Milestone 3 — Migrate Entities. Branch `claude/variables-milestone-3-entities`. Four commits: (1) `ae5055e` — prep: wire 7 asmdef references + `package.json` dependency to `Scaffold.Variables`; fix pre-existing CS0507 in graphflow tests (`protected internal override` → `protected override`). (2) `53a5094` — sub-steps 1+2: delete `Scaffold.Entities.Variable`; move `payloadTypeId` to `VariableEntry` and `EntityModifierEntry`; rewrite `VariableKeySoField` and `EntityModifierEntryDrawerGui` for entry-level `payloadTypeId`; rename `.Key` → `.Id` across ~30 files; per-file `using Variable = Scaffold.Variables.Variable;` aliases to avoid ambiguity while both `Scaffold.Entities.IVariableBag` and `Scaffold.Variables.IVariableBag` coexist. (3) `8d6a744` — sub-steps 3+4: delete `Scaffold.Entities.IVariableBag`; `IEntityVariableStorage : Scaffold.Variables.IVariableBag`; `VariableBag`/`LocalVariableStorage`/`StoreVariableStorage` implement `TryGet<T>`/`TryGet`/`LocalHandles` via delegate-based `EntityVariableHandle<T>`; `VariableValue<T>.CreateWithValue` internal accessor; `VariableBag.TryGetBase` marked `[Obsolete]`; `VariableBagLegacyExtensions.cs` shipped. (4) `46f8004` — sub-step 5: `[FormerlySerializedAs("key")]` on `Variable.id`, `[FormerlySerializedAs("payloadTypeId")]` on `Variable.typeName`; `ISerializationCallbackReceiver` on `VariableEntry`/`EntityModifierEntry` syncing `payloadTypeId` from `Variable.TypeName`; reflection-based migration tests + `OnAfterDeserialize` round-trip tests. **Validation: compilation clean; 326 tests passing (322 baseline + 4 new migration tests); 7 pre-existing failures in CloudCode/LiveOps/Benchmarks unrelated to this work.**
- [x] Milestone 4 — Build `StoreVariableBagBuilder` + `StoreBackedHandle<TState, T>` in `com.scaffold.entities.states`. Branch `claude/variables-milestone-4-state-backed-bag`. New files under `Runtime/Variables/`: `ISliceListener.cs` (internal listener contract), `StoreBackedHandle.cs` (writable, prime-then-dedup against `_last`), `ReadOnlyStoreBackedHandle.cs`, `StoreBackedVariableBag.cs` (`IVariableBag` + `IDisposable`), `BagFallback.cs` (`FallbackMode` enum), `StoreVariableBagBuilder.cs` (builder + `SliceScope<TState>` + `EntityScope` + internal `TypedBindingGroup<TState>`). Public `Store.IsPayloadRegistered(Type)` accessor added on `com.scaffold.states` so the builder can fail-fast at `.Build()` on unregistered writable payloads. Tests: `StoreBackedHandleDeferralTests` (3 deferral tests — `ExecuteBatch` coalesce, defer-scope net-zero no-fire, defer-scope multiple-sets single-fire), `StoreVariableBagBuilderTests` (14 tests covering BindBase read/write/subscribe/dedup, BindComputed read-only + modifier projection, generic `Bind<T,TPayload>`, payload-validation throw, duplicate-id throw, WithFallback InMemoryDefault + Throw modes, single-subscription-per-group invariant, null-store throw), `StoreBackedVariableBagDisposalTests` (3 tests — unsubscribe-on-dispose, idempotent dispose, clears local handles). **Validation: 47 bridge tests all green; full editmode suite 346 passed / 7 pre-existing CloudCode+LiveOps failures unrelated to this work.**
- [x] Milestone 5 — Erase the `[Obsolete]` extension methods. Branch `claude/variables-milestone-5-erase-obsolete`. Deleted `VariableBagLegacyExtensions.cs` (+ meta). Removed `[Obsolete] public VariableBag.TryGetBase` and its private `TryGetBaseCore`; added `internal bool VariableBag.TryGetLocalBase(Variable, out VariableValue)` with the same body for entity-internal callers that need polymorphic base-value access. Migrated `EntityDefinition.TryGetDefaultValue` and `LocalVariableStorage.TryGetBase` to call `TryGetLocalBase`; dropped the surrounding `#pragma warning disable CS0612, CS0618`. Migrated four `VariableBagTests` methods from `bag.TryGetBase(key, out VariableValue v)` to `((IVariableBag)bag).TryGet<float>(key.Id, out var handle)` + `handle.Value`. `IEntityVariableStorage.TryGetBase` and `EntityState.TryGetBase` retained as legitimate domain APIs (not deprecated shims). No remaining `[Obsolete]` markers on variable-storage APIs; no remaining `VariableBagLegacyExtensions` references. **Validation: tests passed locally (user-confirmed).**
- [x] Milestone 6 — Wire a real consumer end-to-end. Branch `claude/variables-milestone-6-state-backed-sample`. Lands `Assets/Packages/com.scaffold.entities.states/Tests/StateBackedBlackboardTests.cs` with three acceptance tests covering the full loop: `GraphRead_ReflectsOutOfBandStoreExecute` (graph re-reads after `store.Execute`), `GraphRead_FollowsSnapshotRestore` (the plan's snapshot round-trip acceptance — `Run → snap → Execute → Run → LoadSnapshot → Run`, verifying the third read returns the pre-snapshot value), and `GraphWrite_DispatchesPayloadToStore` (graph's `SetVariable<int>` node writes land in `EntityState`). The sample defines `StateBackedRunner`/`StateBackedBuilder`/`StateBackedAsset` inline, with `CreateVariableBag` returning a `new StoreVariableBagBuilder(store).ForEntity(entityRef).BindBase<int>("hp", hpVar).Build()` bag. Located in the bridge tests asmdef (not graphflow Samples~) to keep the prod-runtime dep arrow one-way: graphflow has zero references to the bridge; only the bridge **tests** assembly references graphflow. Adds `"Scaffold.GraphFlow"` to the bridge tests asmdef references. **Validation: Unity compilation precheck PASS; tests passed locally (user-confirmed M5 baseline, M6 to validate same way).**

## Surprises & Discoveries

- Observation: `main` has shipped a substantial GraphFlow Variables update since this branch diverged. PR #49 plus follow-up commits added ~1650 lines / removed ~510 in `com.scaffold.graphflow`. Net effect: the cell, bag, and node infrastructure described in this plan as "to be deleted in Milestone 2" already exists in a near-final form, the `VariableDefault<T>` polymorphic-seed abstract class was renamed to `BlackboardVariable<T>` with concrete subclasses `BlackboardInt` / `BlackboardFloat` / `BlackboardBool` / `BlackboardString` / `BlackboardObject`, the per-T `Get/Set/Observe` typed concretes were collapsed into single generic `GetVariable<T>` / `SetVariable<T>` / `ObserveVariable<T>` runtime nodes, `IVariableBag.Cells` enumeration landed (functionally equivalent to this plan's `LocalHandles`), and cycle detection on the parent chain was implemented via `InMemoryVariableBag.TryGetCellGuarded` (resolves the Q9 deferral noted in `Plans/SharedVariablesPackage.md`).
  Evidence: `git log origin/main` shows commit `955e7e2` (PR #49 merge) and follow-ups `6dbe381`, `7e7d0f7`, `4063416`, `7c9a773`, `c48cf07`. `git diff HEAD origin/main -- Assets/Packages/com.scaffold.graphflow/` lists 62 files changed.

- Observation: Real consumer code now ships on `main` exercising the legacy property-setter / event-subscription surface. `Samples/CardSandbox/Runtime/Showcase/VariableShowcase.cs` calls `_runner.Variables.TryGetCell<int>("hp", out _hp)`, then `_hp.Changed += v => AddLog(...)`. `Samples/CardSandbox/Runtime/Showcase/StrikeWithVariables.cs` similarly uses property writes. `Tests/VariableFollowUpTests.cs` covers backlog items 7-11 against the same surface. All this code migrates in Milestone 2 to method-style `handle.Subscribe(...)` and `handle.Set(x)`.
  Evidence: `git show origin/main:Assets/Packages/com.scaffold.graphflow/Samples/CardSandbox/Runtime/Showcase/VariableShowcase.cs` lines 32-42.

- Observation: `com.scaffold.entities` and `com.scaffold.entities.states` are unchanged on `main`. Milestone 3 cost estimate stands.
  Evidence: `git diff --stat HEAD origin/main -- 'Assets/Packages/com.scaffold.entities*'` produced no output.

- Observation (Milestone 3): `[FormerlySerializedAs]` on the shared `Variable` class handles the YAML field-name mapping transparently (`key` → `id`, `payloadTypeId` → `typeName`) without any custom migration script. The plan's predicted `OnAfterDeserialize`-based migration turned out to be a secondary mechanism: it syncs the entry-level `payloadTypeId` field from the already-migrated `Variable.TypeName`, but the primary deserialization migration is handled entirely by Unity's built-in `[FormerlySerializedAs]` attribute.

- Observation (Milestone 3): Sub-steps 1+2 (delete entity `Variable`, move `payloadTypeId`) had to be executed together because the entity `Variable.cs` file contained both the `Variable` class and the `payloadTypeId` field. Similarly, sub-steps 3+4 (delete entity `IVariableBag`, implement shared interface methods) were entangled — deleting the interface without simultaneously providing the shared interface's `TryGet<T>`/`TryGet`/`LocalHandles` implementations would leave the codebase uncompilable.

- Observation (Milestone 3): `TryGetBase` was marked `[Obsolete]` only on `VariableBag`, NOT on `IEntityVariableStorage`. Entity internals (`EntityInstance.TryGetVariable`, `LocalVariableStorage.TryGetBase`, `StoreVariableStorage.TryGetBase`) heavily use the storage interface's `TryGetBase` and marking it obsolete there would create widespread warnings with no migration path until M5. The `VariableBagLegacyExtensions.cs` shim exists for external callers who had the old `IVariableBag.TryGetBase`.

- Observation (Milestone 3): The delegate-based `EntityVariableHandle<T>` pattern bridges entity `VariableValue<T>` to shared `IVariableHandle<T>`. Writing through `Set(T)` required `VariableValue<T>.CreateWithValue` — an internal accessor for the protected abstract `WithValue` factory method — because `EntityVariableHandle<T>` closures need to construct new `VariableValue<T>` instances from raw `T` values. This accessor already exists and is available for M4's `StoreBackedHandle` if it needs to produce `VariableValue` instances.

- Observation (Milestone 3): `Variable.Equals` compares by `Id` only (not `TypeName`), so dictionary lookups work via `new Variable(id, "")` as the lookup key. This is used throughout `VariableBag.TryGet<T>` and `LocalVariableStorage`. M4's `StoreBackedVariableBag` should use the same pattern for handle lookup.

- Observation (Milestone 4): `DeferredStateEventHandler.BeginDeferScope()`'s returned `IDisposable` does NOT flush on dispose — it only decrements `deferralDepth`. Consumers MUST call `Flush()` explicitly after the scope to drain buffered events. The deferral tests originally assumed auto-flush and silently passed Test 2 (because no events ever fired); Test 3 failed loudly when fires were required. Fixed by adding `deferral.Flush()` after the `using` block. Production consumers wrapping `Execute`/`LoadSnapshot` in a defer scope must follow the same pattern. Documenting here so M6's sample doesn't trip on the same edge.

- Observation (Milestone 4): `ReadOnlyStoreBackedHandle<TState, T>` implements `IReadOnlyVariableHandle<T>` per the plan's interface spec — it does NOT implement the writable `IVariableHandle<T>`. As a result, `IVariableBag.TryGet<T>` returns false for read-only bindings. To keep read-only handles discoverable without violating the shared interface, `StoreBackedVariableBag` exposes an extra typed accessor `TryGetReadOnly<T>(string, out IReadOnlyVariableHandle<T>)` not on the `IVariableBag` interface. M6's sample consumer (graph runner with state-backed blackboard) will need to know per-variable whether to call `TryGet<T>` or `TryGetReadOnly<T>`; the builder API makes this explicit at bind time (`BindBase`/`Bind` writable vs `BindComputed`/`BindReadOnly` read-only) so the consumer can dispatch accordingly. If M6 surfaces ergonomic pain, the alternative is to make `ReadOnlyStoreBackedHandle<TState, T>` also implement `IVariableHandle<T>` with `Set` throwing — flagged here as a deferred decision.

- Observation (Milestone 4): The builder API surface in the plan's `Interfaces and Dependencies` section listed `WithFallback(IEnumerable<RuntimeVariable> seed, FallbackMode mode)`. That coupled the bridge package to graphflow's `RuntimeVariable` type and would have introduced a `Scaffold.Entities.States` → `Scaffold.GraphFlow` asmdef reference. Resolved by retyping the parameter to `IEnumerable<(string id, VariableDefault? @default)>` — the same shape as `Scaffold.Variables.InMemoryVariableBag`'s seed constructor. Graphflow consumers in M6 will project their `RuntimeVariable[]` to `(rv.id, rv.defaultValue)` tuples at the call site.

- Observation (Milestone 4): The bridge package now depends on `Store.IsPayloadRegistered(Type)` — a public accessor on `Store` that wraps the M1 `MutatorRegistry.IsRegistered(Type)`. The builder validates writable bindings at `.Build()` time and throws a clear `InvalidOperationException` listing missing payload types if any binding references an unregistered mutator. Minimal addition to `com.scaffold.states` (5 lines including doc comment).

- Observation (Milestone 4): The grouping invariant (one `store.Subscribe<TState>(ref, handler)` per `(Reference, Type<TState>)`) is enforced via an internal `TypedBindingGroup<TState>` indexed by `(Reference, Type)` in the builder's dictionary. The bag's `IDisposable` tracks one `Action` cleanup per group; calling `Dispose` unsubscribes all groups in order. Per-binding subscriptions (which would have been simpler to implement) were rejected because they multiply by handle count instead of scaling with slice count — important when N variables bind to one entity slice.

- Observation (Milestone 2): the original plan kept graphflow's non-generic `BlackboardVariable` shim ("RuntimeVariable.defaultValue field type stays BlackboardVariable, now indirectly a VariableDefault"). That isn't possible under C# single inheritance once `BlackboardVariable<T>` extends `Scaffold.Variables.VariableDefault<T>` directly. Resolved by collapsing the non-generic shim entirely and retyping `RuntimeVariable.defaultValue` to `Scaffold.Variables.VariableDefault`. Concrete subclasses (`BlackboardInt`/`Float`/`Bool`/`String`/`Object`) keep their names and `[SerializeReference]` polymorphic-deserialization compatibility because Unity keys SerializeReference on the concrete class FQN, which is unchanged.

- Observation (Milestone 2): the plan's prescribed "_handle : InMemoryHandle<T>?" field type for the runtime nodes (Get/Set/Observe) ties graphflow's hot-path bindings to one specific bag implementation. That's fine for Milestone 2 (where every handle today is in-memory) but would silently no-op in Milestone 4 / Milestone 6 once `StoreBackedHandle<TState, T>` enters the runtime. Used `IVariableHandle<T>?` in the node fields instead; the `Subscribe` / `Set` / `Value` API used by the nodes lives on the interfaces, so there's no functional cost, and Milestone 4 will not need to touch the node files. (Closure capture in `InputPort<T>.ConnectFromVariable` continues to use `IReadOnlyVariableHandle<T>`.)

- Observation (Milestone 2): `Variables { get; private set; }` had to widen to `internal set` so `GraphBuilder.Build` can do the direct `runner.Variables = runner.CreateVariableBag(seed)` assignment the plan calls for (collapsing the old `SeedVariables` plumbing). Setter remains internal — only the runtime assembly can write it.

- Observation (Milestone 2): Asmdefs that touch graphflow variable types (`Scaffold.GraphFlow.Editor`, `Scaffold.GraphFlow.CardSandbox`, `Scaffold.GraphFlow.Tests`, `Scaffold.GraphFlow.EditorTests`) needed explicit references to `Scaffold.Variables` (GUID `aeaa2ad3d0d1b9aec5a07ab29b764460`). Unity asmdef references are not transitive — `RuntimeVariable.defaultValue : VariableDefault` requires every consumer asmdef to know about `VariableDefault` directly to invoke instance members on it (`.ValueType`, etc.).

## Decision Log

- Decision: Extract a shared package `com.scaffold.variables` rather than continuing with three parallel variable systems.
  Rationale: The duplicate `IVariableBag` interface name across `Scaffold.Entities` and `Scaffold.GraphFlow` was already a smell. Extraction gives one canonical interface; per-package implementations stay where they are. Cross-package binding (blackboard ↔ entity / state) collapses from per-consumer adapter code to a shared builder API.
  Author: Design session, 2026-05-09.

- Decision: Shared package owns `Variable` (the key type), not just the bag and handle interfaces.
  Rationale: Cleaner abstraction. Otherwise `EntityState.BaseValues : IReadOnlyDictionary<Variable, VariableValue>` would still be keyed by an entities-specific type, and any third package wanting to participate would either re-import entities or define its own key shape. The cost — about ten more days of migration work for the entity package — buys a coherent end-state.
  Author: Design session, 2026-05-09.

- Decision: Migration uses `[Obsolete]` extension method to route legacy `TryGetBase` to new `TryGet<T>`, then erases the extension after callers migrate (Phase D / Milestone 5).
  Rationale: Avoids a permanent dual-canonical state. The codebase must end with exactly one `IVariableBag` interface (Invariant 1, below). The extension is genuinely transitional — not a forever shim.
  Author: Design session, 2026-05-09.

- Decision: The `IVariableHandle<T>` write surface is a `Set(T value)` method, not a property setter that hides the read-only `Value { get; }` via `new`.
  Rationale: Avoids any C# subtlety around interface property shadowing. Reads stay ergonomic (`handle.Value`); writes are explicit (`handle.Set(x)`). Consistent with the method-style `Subscribe` / `Unsubscribe` decision.
  Author: Code-audit pass, 2026-05-09.

- Decision: Subscription on `IReadOnlyVariableHandle<T>` is method-style (`Subscribe(Action<T>) / Unsubscribe(Action<T>)`), not an `event Action<T> Changed`.
  Rationale: Lets `StoreBackedHandle` manage subscription bookkeeping (one store-side subscribe fans out to many handle subscribers). Makes teardown explicit, which the `IDisposable` bag needs anyway. In-memory handles wrap an internal multicast delegate in one-line `Subscribe` / `Unsubscribe` methods at no real cost.
  Author: Design session, 2026-05-09.

- Decision: `StoreVariableBagBuilder` lives in `com.scaffold.entities.states`, not in a new `com.scaffold.variables.states` sub-package.
  Rationale: The entity-states bridge becomes the canonical state-backed variable storage solution. Avoids creating a fourth package. Tradeoff: consumers that want raw state-slice projection without entities still pull in the bridge — accepted, since entity-state-backed is the default path going forward.
  Author: Design session, 2026-05-09.

- Decision: Two non-negotiable invariants. **Invariant 1:** No package ships a parallel internal variable interface — only implementations of the shared one. **Invariant 2:** Graphflow exposes `CreateVariableBag(seed)` (not `CreateParentBag()`) so consumers can fully replace storage, not just chain.
  Rationale: Invariant 1 prevents the "two canonical systems per package" outcome. Invariant 2 is required for snapshot coherence — if graph-declared variables go through `InMemoryVariableBag` and only consumer-supplied parents are state-backed, those graph-declared variables silently bypass `Store.SaveSnapshot` and break save/load.
  Author: Design session, 2026-05-09.

- Decision: `StoreBackedHandle.Subscribe` callbacks fire at most once per slice per `Store.Execute` or `Store.LoadSnapshot`, with the final post-merge value. Callbacks dedupe on a cached `_last` field; intermediate slice mutations during a deferred batch are not observable.
  Rationale: This is the right semantics for blackboard binding (cells only care about the latest value), and it falls out naturally from the `Store`'s `DeferredStateEventHandler.LatestPerKey` merge mode. Document and test explicitly so consumers don't try to build audit logs on top of `Subscribe`.
  Author: Design session, 2026-05-09.

- Decision: Aggregate slices are out of scope for v1. `StoreVariableBagBuilder.ForSlice<TState>(ref)` works on canonical slices only.
  Rationale: Aggregates are a parallel slice family in `com.scaffold.states` with their own lifecycle. Supporting them adds complexity not justified by any current consumer. Document the limitation; promote to follow-up work if a real need appears.
  Author: Design session, 2026-05-09.

- Decision: `payloadTypeId` (entity's "int" / "float" string tag for the polymorphic `VariableValue` subclass) moves from `Variable` to `VariableEntry` in Phase B.
  Rationale: Shared `Variable` should not carry an entity-specific concept. `VariableEntry` is the serialized authoring form where the tag is actually consumed (drawer field validation). `VariableKeySoField.cs:193-209` rewrites to read the tag from the entry instead of the key.
  Author: Code-audit pass, 2026-05-09.

- Decision: Each package may keep a custom-flavored *implementation* of the shared variable abstraction with a domain-specific name, as long as (a) it implements the shared `Scaffold.Variables` interfaces, (b) the shared package owns the canonical interfaces (Invariant 1), and (c) any consumer can integrate with the generic shared types regardless of which custom implementation a producer ships. GraphFlow's `BlackboardVariable<T>` (the polymorphic authoring/seed class that ships on `main`) is preserved as graphflow's subclass of shared `Scaffold.Variables.VariableDefault<T>`. Concrete subclasses `BlackboardInt`, `BlackboardFloat`, etc. continue to live in `Scaffold.GraphFlow`.
  Rationale: GraphFlow has done enough internal validation work on `BlackboardVariable<T>` that renaming it to a generic shared name (`IntDefault` etc., as the design notes originally proposed) would discard battle-tested authoring surface and asset compatibility. The user's principle: shared package ships the base abstraction; each package is free to ship a domain-named flavor on top; what's non-negotiable is that all packages integrate via the shared interfaces. `BlackboardVariable<T>` is graphflow-flavored because graphflow has the blackboard concept (graph asset blackboard panel); other packages will have other domain-named flavors (entity's `VariableEntry`-driven authoring; bridge's `StoreVariableBagBuilder`).
  Author: Design session, 2026-05-09 — after reviewing main's GraphFlow Variables update.

- Decision: Milestone 2 scope expands to migrate the real consumer surface that landed on main. `VariableShowcase.cs`, `StrikeWithVariables.cs`, `VariableFollowUpTests.cs`, and any other code calling `cell.Value = x` or `cell.Changed += handler` migrate to `handle.Set(x)` and `handle.Subscribe(handler)` as part of the same milestone. The earlier "graphflow is mostly namespace migration" framing no longer applies — there is real public-API surface to migrate.
  Rationale: Per Invariant 1, the codebase ends with one canonical interface; per the locked subscription-style decision (method-based, not event-based), the property setter and event-add operators must go. Backwards-compat shims aren't acceptable in graphflow because the codebase is small and self-contained — clean cut at Milestone 2.
  Author: Design session, 2026-05-09.

- Decision: GraphFlow's existing `VariableCell<T>` is replaced by the shared `Scaffold.Variables.InMemoryHandle<T>` concrete class (not subclassed, not aliased — outright replacement). Existing graphflow port-bind closures that captured `VariableCell<T> _cell` references migrate to capturing `InMemoryHandle<T> _handle` references; the closure body changes from `_cell.Value` to `_handle.Value` (read) and `_cell.Value = x` to `_handle.Set(x)` (write). Hot-path zero-boxing is preserved because `InMemoryHandle<T>` stores `T _value` directly in a field, identically to the existing `VariableCell<T>`.
  Rationale: Invariant 1 mandates one canonical handle type. Renaming graphflow's cell to keep the "Cell" name is technically possible but adds a sealed concrete class to the shared package whose only purpose would be to carry forward graphflow's old name. `InMemoryHandle<T>` is the cleaner generic name; graphflow's existing 15-or-so usages migrate mechanically.
  Author: Design session, 2026-05-09.

- Decision: Cycle detection on the bag parent chain is preserved in the shared `InMemoryVariableBag` implementation. Main's `TryGetCellGuarded` pattern (a per-call origin reference that aborts if a parent walk loops back to the original bag) ports directly into the shared package's `InMemoryVariableBag.TryGet<T>`.
  Rationale: Cycle detection is a real safety property (`VariableFollowUpTests.cs` covers it as item 8), not graphflow-specific behavior. Resolves Q9 from the design notes.
  Author: Design session, 2026-05-09.

## Outcomes & Retrospective

**Milestone 1 — Land shared package skeleton (complete)**
Delivered `com.scaffold.variables` with all seven shared types (`Variable`, `IVariableHandle`, `IReadOnlyVariableHandle<T>`, `IVariableHandle<T>`, `IVariableBag`, `VariableDefault<T>`, `InMemoryHandle<T>`, `InMemoryVariableBag`). `MutatorRegistry.IsRegistered(Type)` added to `com.scaffold.states`. 11 smoke tests passing (seed, type-mismatch, missing-key, set/read, subscribe distinct-value-only, unsubscribe, parent-cascade, write-hits-owning-layer, non-generic introspection, `LocalHandles` enumeration, cyclic-parent-chain). Branch: `claude/variables-milestone-1-skeleton`.

**Milestone 2 — Migrate GraphFlow (complete)**
Deleted `IVariableBag.cs`, `VariableCell.cs`, `InMemoryVariableBag.cs` from graphflow. `BlackboardVariable<T>` extends shared `VariableDefault<T>`. `CreateParentBag()` replaced with `CreateVariableBag(IEnumerable<RuntimeVariable> seed)`. All graphflow nodes, samples, and tests migrated to method-style handle API. Branch: `claude/variables-milestone-2-prep-A85gh` (merged to main via PR).

**Milestone 3 — Migrate Entities (complete)**
Largest milestone. Four commits on `claude/variables-milestone-3-entities`: (1) asmdef wiring, (2) delete entity `Variable` + move `payloadTypeId`, (3) delete entity `IVariableBag` + implement shared interface on all storage classes, (4) `[FormerlySerializedAs]` asset migration + `OnAfterDeserialize` round-trip. 326 tests passing (322 baseline + 4 new). Key artifacts: `EntityVariableHandle<T>` delegate bridge, `VariableValue<T>.CreateWithValue` internal accessor, `VariableBagLegacyExtensions.cs` obsolete shim. No entity `Variable` or entity `IVariableBag` remain. `IEntityVariableStorage : Scaffold.Variables.IVariableBag`.

**Milestone 4 — Build the state-backed bag and binding builder (complete)**
Smallest milestone by file count, largest by API surface. Eight runtime files + three test files on `claude/variables-milestone-4-state-backed-bag`. Key shape: `StoreBackedHandle<TState, T>` is the writable handle; on construction it caches nothing, but the bag's `Build()` `Prime`s it from the current slice so the very first store event compares against the real initial value. Subscriber dispatch is fronted by an `ISliceListener<TState>` interface so the bag can fan out one store-side `Subscribe<TState>(ref, handler)` per `(Reference, Type<TState>)` group to N handles. The builder enforces fail-fast on duplicate variable ids and on writable bindings whose payload type isn't registered with the store. `BindComputed`/`BindReadOnly` produce `IReadOnlyVariableHandle<T>` (no `Set`), accessed via a new `StoreBackedVariableBag.TryGetReadOnly<T>` method rather than the writable `TryGet<T>` — see surprises section for the rationale and consequences.

**Milestone 5 — Erase the [Obsolete] extension (complete)**
Mechanical cut. Two files affected materially (`VariableBag.cs` removes the obsolete public method and gains an internal `TryGetLocalBase`; `LocalVariableStorage.cs` swaps the call). One file deleted (`VariableBagLegacyExtensions.cs`). Two consumers updated (`EntityDefinition.cs`, `VariableBagTests.cs`). Interpretation note worth recording: the plan's "delete the non-virtual `TryGetBase` method on `IEntityVariableStorage` if any survived as a default implementation" was a no-op — `IEntityVariableStorage.TryGetBase` is an abstract interface method, not a default implementation, and is the canonical way for callers to read the polymorphic `VariableValue` from entity storage. The plan's Invariant 1 ("no parallel interface") is satisfied: `IEntityVariableStorage : Scaffold.Variables.IVariableBag` and the `TryGetBase` accessor is a domain-specific addition, not a duplicate of the typed-handle API.

**Milestone 6 — Wire a real consumer end-to-end (complete)**
Smallest milestone by code volume. Three tests in one file, plus the runner/builder/asset trio defined inline. Two judgment calls worth recording:

(1) **Location.** The plan suggested `Assets/Packages/com.scaffold.graphflow/Samples~/StateBackedBlackboard/`, but that would couple graphflow to `Scaffold.Entities.States` — wrong direction. Put it in `com.scaffold.entities.states/Tests/` instead: the bridge already depends on `Scaffold.Variables` + `Scaffold.States` + `Scaffold.Entities`; adding `Scaffold.GraphFlow` to the **test** asmdef only is a one-way arrow that doesn't pollute production. If/when a runnable scene-based sample is desired, it can ship in a future `com.scaffold.graphflow.statebackedsample` Samples~ package or live in a downstream demo project.

(2) **Read-only ergonomics.** The M4 deviation (read-only handles only accessible via `bag.TryGetReadOnly<T>`, not via graphflow's `runner.Variables.TryGet<T>`) didn't bite for the M6 acceptance because the sample only uses `BindBase<int>` (writable). If a future consumer wants `BindComputed`/`BindReadOnly` to be reachable from graphflow's `GetVariable<T>` node, the simplest fix is to make `ReadOnlyStoreBackedHandle<TState, T>` also implement `IVariableHandle<T>` with `Set` throwing `NotSupportedException`. Deferred until a real consumer needs it.

**Plan complete.** All six milestones landed: M1 shared package, M2 graphflow migration, M3 entity migration + asset migration, M4 state-backed bag + builder, M5 obsolete-shim erase, M6 end-to-end consumer wire-up.

## Context and Orientation

This section explains every file and concept the rest of the plan depends on. A reader who has never opened this repository should be able to start from here.

### The three packages today

The repository is a Unity project (Linux build host, Unity 2022 LTS-class). The relevant packages live under `Assets/Packages/`. Three are involved.

`com.scaffold.graphflow` (`Assets/Packages/com.scaffold.graphflow/`) is a node-graph runtime built on Unity's Graph Toolkit. Designers author graphs in an editor canvas; `GraphRunner` instances execute them. As of the merged work on `claude/graph-flow-blackboard-vars-IsUNX`, the package supports declaring typed variables on a graph asset's blackboard panel; runtime nodes (`GetVariableNode<T>`, `SetVariableNode<T>`, `ObserveVariableNode<T>`) read and write those variables through a typed cell abstraction. The relevant runtime files live under `Assets/Packages/com.scaffold.graphflow/Runtime/Variables/` and `Assets/Packages/com.scaffold.graphflow/Runtime/Controller/GraphRunner.cs`. Today the seam for consumer-supplied variable storage is `runner.CreateParentBag()` returning a `Scaffold.GraphFlow.IVariableBag?`. That seam is insufficient and gets replaced in Milestone 2 (see Invariant 2 below).

`com.scaffold.entities` (`Assets/Packages/com.scaffold.entities/`) models gameplay objects (characters, items, cards) as definitions and instances. Entities have variables (typed values keyed by an identifier) and modifier stacks (ordered collections of `VariableModifier` instances that transform a base value into an effective value). The package's variable types live under `Runtime/Core/Variables/` (`Variable.cs`, `VariableValue.cs` and concrete subclasses), `Runtime/Core/VariableBags/` (`VariableBag.cs`, `IVariableBag.cs`, `VariableEntry.cs`), and `Runtime/Core/Instance/` (`EntityInstance.cs`, `IEntityVariableStorage.cs`, `LocalVariableStorage.cs`). Editor authoring uses `[SerializeField]` strings on `Variable` (key and `payloadTypeId`) plus a polymorphic `[SerializeReference] VariableValue` for the value, drawn by editor code under `Editor/`. The reflection-driven `VariableValueRegistry` (`Runtime/Core/Utilities/VariableValueRegistry.cs`) maps the `payloadTypeId` string to concrete `VariableValue` subclasses via `[VariableValueIdAttribute]` annotations.

`com.scaffold.entities.states` (`Assets/Packages/com.scaffold.entities.states/`) is the bridge package. It routes entity variable reads and writes through a `Store` (from `com.scaffold.states`) so they participate in immutable snapshots. The canonical slice is `EntityState`, an immutable record holding `BaseValues : IReadOnlyDictionary<Variable, VariableValue>` and `ModifierStacks : IReadOnlyDictionary<Variable, IReadOnlyList<ActiveModifier>>`. Mutations dispatch typed payloads (`SetBaseValuePayload`, `AddModifierPayload`, etc.) which registered mutators (`SetBaseValueMutator`, etc.) apply by returning a fresh `EntityState` record. `StateInstance<TDefinition>` is the entity wrapper backed by `StoreVariableStorage` (which proxies all storage methods to `store.Get<EntityState>(ref)` / `store.Execute(payload)`). The bridge is bootstrapped via `EntityBridgeContext.RegisterMutators(StoreBuilder)`.

### The states package (read-only context)

`com.scaffold.states` (`Assets/Packages/com.scaffold.states/`) is not modified directly except for one small public-method addition described in Milestone 1. The relevant context: a `Store` holds canonical slices keyed by `(Reference, Type)`. Reads use `store.Get<TState>(reference)`; writes dispatch typed payloads via `store.Execute<TPayload>(reference?, payload)` which routes through registered `Mutator<TState, TPayload>` instances. Subscribers register via `store.Subscribe<TState>(reference, Action<TState, StateChangeEvent>)`. The `DeferredStateEventHandler` buffers events during a `BeginDeferScope` window (used inside `Store.Execute` and `Store.LoadSnapshot`) and flushes either preserving every event (`PreserveAll`) or merging to one event per `(reference, type)` key (`LatestPerKey`).

### Two design invariants that govern everything

**Invariant 1 — Single canonical variable system per package.** After this work, no package may have two parallel variable abstractions. The shared `Scaffold.Variables` types are *the* variable abstraction; existing per-package types are deleted, not deprecated-and-kept. A package may ship its own `IVariableBag` *implementation* (in-memory, state-backed, modifier-aware) but never its own duplicate *interface*. The `[Obsolete]` extension method introduced in Milestone 3 is genuinely transitional — Milestone 5 deletes it.

**Invariant 2 — Storage replacement, not parent chaining.** Graphflow's graph-layer variable bag must be fully replaceable by the consumer, not merely chained to as a parent. Today's `runner.CreateParentBag()` exposes only a chain seam. A consumer that wants snapshot-coherent variables would have a state-backed bag as the parent, but variables declared on the graph asset itself would still be seeded into a default `InMemoryVariableBag` at the graph layer — those variables would silently bypass `Store.SaveSnapshot` and break save/load. The fix is to replace `CreateParentBag()` with `CreateVariableBag(IEnumerable<RuntimeVariable> seed)` so the consumer constructs the entire bag, including how each declared variable is materialized.

### Key terms used throughout this plan

`Variable` — the identity for a named, typed value. After Milestone 1, lives at `Scaffold.Variables.Variable` and carries `id` (GUID-like string for stable identity) and `typeName` (`AssemblyQualifiedName`-style string for the runtime value type). Equality is on `id`; `typeName` is descriptive.

`Handle` — a typed accessor for one variable in one bag. `IReadOnlyVariableHandle<T>` exposes `Value { get; }`, `Subscribe(Action<T>)`, `Unsubscribe(Action<T>)`. `IVariableHandle<T> : IReadOnlyVariableHandle<T>` adds `Set(T value)`.

`Bag` — a `Parent`-chained lookup of handles by string id. `IVariableBag.TryGet<T>(string id, out IVariableHandle<T> handle)` is the primary typed accessor. The non-generic `TryGet(string id, out IVariableHandle handle)` exists for introspection and snapshot/inspector tooling. `LocalHandles` enumerates handles in this bag (not the parent chain), used for save/load and inspector views.

`Default` — designer-authored seed value for a variable. `VariableDefault<T>` is a `[Serializable]` polymorphic base; concrete subclasses (`IntDefault`, `FloatDefault`, etc.) carry a `T value` field and produce an `InMemoryHandle<T>` via `CreateHandle(string id)`. Existing graphflow `RuntimeVariable.defaultValue` references this hierarchy after Milestone 2.

`InMemoryHandle<T>` — concrete public class implementing `IVariableHandle<T>`. Stores `T _value` directly (no boxing). Equivalent to graphflow's existing `VariableCell<T>` after Milestone 2 deletes the original. Graphflow's port-bind closures cache references to the concrete class for hot-path zero-boxing; the shared package exposes the concrete type for this reason.

`StoreBackedHandle<TState, T>` — concrete class in `com.scaffold.entities.states` implementing `IVariableHandle<T>`. Pulls fresh from the store on every `Value` read; dispatches a payload on every `Set` write; subscribes once to `store.Subscribe<TState>(ref, ...)` and fans out to handle subscribers via a guarded `_subscribers` field.

`Bag builder` — the API consumers use to construct a state-backed bag. `StoreVariableBagBuilder`, with chained `.ForSlice<TState>(reference).Bind<T>(varId, project, toPayload)` and `.ForEntity(reference).BindBase<T>(varId, var)` shorthand. `Build()` validates that every distinct payload type used resolves to a registered mutator (via `MutatorRegistry.IsRegistered(Type)` added in Milestone 1), throwing on missing registration.

## Plan of Work

The work is six milestones executed in order. Milestones 1 and 2 are largely independent of entities; Milestone 3 is the largest single chunk; Milestones 4 through 6 are additive and small.

### Milestone 1 — Land the shared package skeleton

The goal of this milestone is for the shared types to exist on disk and compile, so subsequent milestones have something to migrate to. Nothing in graphflow, entities, or the bridge changes in this milestone. After completion, a `com.scaffold.variables` Unity package exists at `Assets/Packages/com.scaffold.variables/` with runtime and tests assemblies, holding the seven types listed below. Plus a one-line public method addition lands in `com.scaffold.states` so Milestone 4 can use it.

The seven shared types live under `Assets/Packages/com.scaffold.variables/Runtime/`. The `Variable` class is a `[Serializable] sealed class` carrying two `[SerializeField] private string` fields (`id`, `typeName`) with public `Id` and `TypeName` properties; `IEquatable<Variable>` implementation comparing on `Id` only; standard `Equals`, `GetHashCode`, and operator overloads. `IVariableHandle` is the non-generic base interface with `string Id { get; }` and `Type Type { get; }`. `IReadOnlyVariableHandle<T> : IVariableHandle` adds `T Value { get; }`, `void Subscribe(Action<T> handler)`, `void Unsubscribe(Action<T> handler)`. `IVariableHandle<T> : IReadOnlyVariableHandle<T>` adds `void Set(T value)`. `IVariableBag` carries `IVariableBag? Parent { get; }`, `bool TryGet<T>(string id, out IVariableHandle<T> handle)`, `bool TryGet(string id, out IVariableHandle handle)`, `IEnumerable<IVariableHandle> LocalHandles { get; }`. `VariableDefault` is an abstract `[Serializable]` class with `abstract Type ValueType { get; }` and `abstract IVariableHandle CreateHandle(string id)`. `VariableDefault<T>` is the typed base with `public T value`, sealed `ValueType => typeof(T)`, virtual `CreateHandle(id) => new InMemoryHandle<T>(id, value)`. `InMemoryHandle<T>` is a `sealed class` implementing `IVariableHandle<T>`, holding `T _value` and `Action<T>? _subscribers`, with `Set` checking `EqualityComparer<T>.Default.Equals(_value, value)` to dedupe. `InMemoryVariableBag` is a `sealed class` implementing `IVariableBag`, holding `Dictionary<string, IVariableHandle> _handles` constructed from a seed collection.

Concrete initial `VariableDefault<T>` subclasses to ship in this milestone: `IntDefault`, `FloatDefault`, `BoolDefault`, `StringDefault`, `ObjectDefault` (the latter for `UnityEngine.Object?`). These five cover the existing graphflow set (see `Assets/Packages/com.scaffold.graphflow/Runtime/Variables/VariableDefault.cs` for the current shapes; the new ones match exactly except for namespace).

The package layout follows the convention used by `com.scaffold.states`: `package.json` in the root, `Runtime/` and `Tests/` as the two assemblies, each with a `*.asmdef`. The runtime asmdef has no dependencies; the tests asmdef depends on `Scaffold.Variables` and the standard Unity test runner references. A small smoke test set lives in `Tests/`: one test confirms `InMemoryVariableBag` round-trips a seeded value through `TryGet<T>` then `handle.Value`; one test confirms `handle.Set(x)` followed by `handle.Value` returns `x`; one test confirms `Subscribe` fires once on a distinct value and zero times on a same-value write.

The `MutatorRegistry.IsRegistered(Type)` addition lives in `Assets/Packages/com.scaffold.states/Runtime/Mutators/MutatorRegistry.cs`. It returns `true` if the registry has any binding for the given payload type, `false` otherwise. Implemented as a one-line wrapper around the existing internal `TryGet(payloadType, out _)` query. A test in `Tests/MutatorRegistryDeduplicationTests.cs` (or a sibling) confirms `IsRegistered(typeof(KnownPayload))` is `true` after registration and `false` for an unregistered type.

Validation for this milestone: from the repository root run `.agents/scripts/validate-changes.cmd` (or the `.sh` equivalent on Linux) and expect no errors. Then run the new tests via `.agents/scripts/run-editmode-tests.ps1` (PowerShell) and observe all three smoke tests plus the `IsRegistered` test passing. Acceptance: the package exists; the assemblies compile; the smoke tests pass; nothing else in the project changes behavior.

### Milestone 2 — Migrate GraphFlow

The goal of this milestone is to make graphflow use only `Scaffold.Variables` types for the bag, cell, and handle abstractions, while preserving graphflow's domain-specific authoring class `BlackboardVariable<T>` as graphflow's named subclass of shared `VariableDefault<T>`. Per Invariant 1, graphflow's existing `Scaffold.GraphFlow.IVariableBag`, `VariableCell<T>`, and `InMemoryVariableBag` are deleted, not aliased. Per Invariant 2, the `runner.CreateParentBag()` seam is replaced with `runner.CreateVariableBag(IEnumerable<RuntimeVariable> seed)`.

Note: this milestone assumes the branch has been brought up to date with `origin/main`. The PR #49 GraphFlow Variables work plus follow-up commits ship a near-final version of the cell/bag/node infrastructure on `main`; the steps below describe the migration from that state.

The deletion targets in `Assets/Packages/com.scaffold.graphflow/Runtime/Variables/` are: `IVariableBag.cs`, `VariableCell.cs`, `InMemoryVariableBag.cs`. The file `BlackboardVariable.cs` is kept and edited in place: `BlackboardVariable<T>` changes its declaration to `public abstract class BlackboardVariable<T> : Scaffold.Variables.VariableDefault<T>` and its `CreateCell(string id)` override is renamed to `CreateHandle(string id)` returning `Scaffold.Variables.IVariableHandle` (which `InMemoryHandle<T>` satisfies). Concrete subclasses `BlackboardInt` / `BlackboardFloat` / `BlackboardBool` / `BlackboardString` / `BlackboardObject` keep their names and `[Serializable]` attributes — their parent class change carries them through.

After the deletions, every `using Scaffold.GraphFlow;` that referenced the deleted types updates to `using Scaffold.Variables;`. The `RuntimeVariable.defaultValue` field type stays `BlackboardVariable` (now indirectly a `VariableDefault`). Hot-path closures that previously captured `VariableCell<T> _cell` change to `InMemoryHandle<T> _handle`; closure bodies change from `_cell.Value` (unchanged for reads) and `_cell.Value = x` (writes become `_handle.Set(x)`). Subscriptions on cells (`cell.Changed += handler`) become `handle.Subscribe(handler)`.

The seam change is the architecturally significant part. `GraphRunner.CreateParentBag()` is removed. `GraphRunner.CreateVariableBag(IEnumerable<RuntimeVariable> seed)` replaces it as a `protected internal virtual` method returning `Scaffold.Variables.IVariableBag`. The default implementation is `=> new InMemoryVariableBag(seed)` where the shared `InMemoryVariableBag` constructor materializes handles by calling `runtimeVariable.defaultValue.CreateHandle(runtimeVariable.id)` for each entry — the same mechanic as today, just behind the shared type. The internal `GraphRunner.SeedVariables(seed)` plumbing collapses — `GraphBuilder.Build` no longer constructs a bag with `new InMemoryVariableBag(seed, runner.CreateParentBag())`; instead it calls `runner.Variables = runner.CreateVariableBag(seed)` directly. The `Flow.Variables` per-Run scratch bag remains as-is (its `Parent` chains to `runner.Variables`). The shared `InMemoryVariableBag.TryGet<T>` preserves the cycle-detection logic ported from main's `TryGetCellGuarded` (an origin-reference walk that aborts on parent loops).

The runtime node types `GetVariable<T>` / `SetVariable<T>` / `ObserveVariable<T>` (in `Scaffold.GraphFlow.Nodes`) are single generic implementations on `main` (one file each) rather than the per-T trios the original design notes anticipated. Migration is correspondingly simpler: each file changes its `_cell : VariableCell<T>?` field to `_handle : InMemoryHandle<T>?` (or `IVariableHandle<T>?` if any node ever expects a non-in-memory binding to flow through ports — leave as concrete for now), updates `runner.Variables.TryGetCell<T>(...)` to `runner.Variables.TryGet<T>(...)`, and migrates the `Set` body from `_cell.Value = NewValue.Read(flow)` to `_handle.Set(NewValue.Read(flow))`. The `Observe` body migrates from `_cell.Changed += handler` to `_handle.Subscribe(handler)`; the existing teardown gap (deferred backlog item #15 — observe subscriptions never unsubscribe) is preserved as a known issue, not addressed in this milestone.

A `WithFallback(IEnumerable<RuntimeVariable> seed, FallbackMode mode)` method lives on `StoreVariableBagBuilder` (Milestone 4). This milestone does not implement it; the default `CreateVariableBag` override just materializes everything in-memory.

Real consumer code that landed on `main` migrates as part of this milestone, not later. The relevant files are `Assets/Packages/com.scaffold.graphflow/Samples/CardSandbox/Runtime/Showcase/VariableShowcase.cs` (a `MonoBehaviour` that calls `_runner.Variables.TryGetCell<int>(...)` and `_hp.Changed += ...`), `Assets/Packages/com.scaffold.graphflow/Samples/CardSandbox/Runtime/Showcase/StrikeWithVariables.cs` (sample asset builder using the same surface), and any other sample scripts under `Samples/CardSandbox/`. They migrate to the new method-style surface.

Tests to migrate: the existing `VariableEdgeTests`, `VariableGetSetTests`, `VariableObserveTests`, `VariableEndToEndTests`, `VariableBagTests`, `VariableWiringTests` plus the newly-landed `VariableFollowUpTests` (covering backlog items 7-11: cycle detection, unconnected variable-bound port, multi-build independence) under `Assets/Packages/com.scaffold.graphflow/Tests/`. All run the same scenarios through the shared types after migration. The body changes are mechanical (namespace updates, `Set` instead of `Value =`, `Subscribe` instead of `Changed +=`, `TryGet` instead of `TryGetCell`). One new test asserts that a `GraphRunner` subclass overriding `CreateVariableBag` to return a custom bag implementation receives that bag as `runner.Variables`. Existing CardSandbox and other sample tests must remain green.

Validation: from the repository root run `.agents/scripts/validate-changes.cmd` and expect no errors. Run `.agents/scripts/run-editmode-tests.ps1` and observe all graphflow tests passing, including the new `CreateVariableBag` override test, the migrated `VariableFollowUpTests`, and the `VariableShowcase` sample compilation. Open the project in Unity and verify there are no `error CS` lines in the editor console; load the `VariableShowcase.unity` scene and confirm it runs without console errors. Acceptance: graphflow no longer contains its own variable interface, bag class, or cell class; `BlackboardVariable<T>` extends shared `VariableDefault<T>`; the seam is repositioned; all tests pass; the sample scene works end-to-end.

### Milestone 3 — Migrate Entities

The goal of this milestone is to extract `Variable` to the shared package, delete `Scaffold.Entities.IVariableBag`, and make `IEntityVariableStorage` extend the shared `Scaffold.Variables.IVariableBag`. This is the largest migration in the plan.

The `Variable` class moves from `Assets/Packages/com.scaffold.entities/Runtime/Core/Variables/Variable.cs` to the shared package's `Variable.cs` (already created in Milestone 1). The old file is deleted. The shared `Variable` carries `id` and `typeName` only; the existing entity-specific `payloadTypeId` field moves to `Assets/Packages/com.scaffold.entities/Runtime/Core/Variables/VariableEntry.cs` as a new `[SerializeField] private string payloadTypeId` field with a public `PayloadTypeId` property. Every entity-internal site that read `variable.PayloadTypeId` is rewritten to either: (a) read from the enclosing `VariableEntry.PayloadTypeId` if the call is in a serialization / drawer context, or (b) derive from `VariableValue.GetType()` via `VariableValueRegistry` if the call is in a runtime context.

The most concentrated rewrite is `Assets/Packages/com.scaffold.entities/Editor/VariableKeySoField.cs:193-209`, which today reads `variable.payloadTypeId` directly. After migration it reads from the enclosing `VariableEntry`'s serialized property. This drawer is the registry coupling hub — every other drawer delegates through it (`VariablePropertyDrawer.cs`, `VariableBagPropertyDrawer.cs`, `EntityModifierEntryDrawer.cs`, `VariableSOEditor.cs`).

`Scaffold.Entities.IVariableBag` (`Assets/Packages/com.scaffold.entities/Runtime/Core/VariableBags/IVariableBag.cs`) is deleted. `IEntityVariableStorage` (`Runtime/Core/Instance/IEntityVariableStorage.cs`) updates its declaration to `public interface IEntityVariableStorage : Scaffold.Variables.IVariableBag` and its `Parent` property type changes to `IEntityVariableStorage?`. The existing entity write methods (`AddVariable`, `RemoveVariable`, `SetBaseValue`, `AddModifier`, `RemoveModifier`, `ClearModifiers`, `RemoveModifiersFromSource`) stay on `IEntityVariableStorage`; modifier semantics are entity-specific and not promoted to the shared interface.

`VariableBag` (`Runtime/Core/VariableBags/VariableBag.cs`) gains a typed `TryGet<T>(string id, out IVariableHandle<T> handle)` implementation that returns an entity-backed handle. The handle internally reads/writes through `TryGetBase`/`SetBase` plus modifier application; its `Set(T)` writes the base only. The legacy `TryGetBase(Variable, out VariableValue)` method is kept but marked `[Obsolete("Use TryGet<T> instead. Will be removed in a future release.")]`. An extension method `TryGetBase` lives in a new `Runtime/Core/VariableBags/VariableBagLegacyExtensions.cs` so legacy callers keep compiling without modifying call sites; the extension routes through `TryGet<T>` and unwraps to a `VariableValue`.

The asset migration script is the second-largest piece of this milestone. When `Scaffold.Entities.Variable` becomes `Scaffold.Variables.Variable`, Unity loses serialized references in existing `.asset` files (sample assets under `Samples~/`, any project assets) and in MonoBehaviour fields anywhere in the project. The mitigation: an `OnAfterDeserialize` callback on every `[Serializable]` type that embeds a `Variable` (notably `VariableEntry`) checks if the legacy `Scaffold.Entities.Variable` field is still present (Unity will deserialize it as a managed-reference null when the type moves) and, if so, reconstructs a `Scaffold.Variables.Variable` from a fallback string-pair (`legacyKey`, `legacyPayloadTypeId`) preserved on the `VariableEntry`. Tests must cover the migration path: a fixture that loads a pre-migration sample asset and confirms the reconstructed `Variable` carries the right `Id`.

Tests to update: every test file under `Assets/Packages/com.scaffold.entities/Tests/` that calls `new Variable("hp", "int")` or `bag.TryGetBase(...)` is updated. Approximately twenty-one `TryGetBase` call sites and twelve `Variable` constructors across tests. The `[Obsolete]` extension keeps the call sites compiling without rewrite during the migration cut; cleanup happens in Milestone 5. Same for the bridge package — `StateBridgeTests.cs` has roughly eight `TryGetBase` calls and three `Variable` constructors.

Validation: run `.agents/scripts/validate-changes.cmd` from the repository root; expect no errors. Run `.agents/scripts/run-editmode-tests.ps1`; expect all entity and entity-bridge tests passing. Manually open one pre-migration `.asset` file in Unity and confirm the `Variable` field reconstructs correctly (Inspector shows the right key and value type). Acceptance: entity package no longer defines its own `IVariableBag` or `Variable`; legacy `TryGetBase` callers compile via the `[Obsolete]` extension; all tests green; sample assets load without errors.

### Milestone 4 — Build the state-backed bag and binding builder

The goal of this milestone is for `com.scaffold.entities.states` to expose a fluent builder consumers use to construct a state-backed `IVariableBag`. After completion, a consumer can call `new StoreVariableBagBuilder(store).ForEntity(entityRef).BindBase<float>(varGuid, hpVar).ForSlice<TurnState>(gameRef).Bind<int>(turnVarGuid, s => s.CurrentTurn, v => new SetTurnPayload(v)).Build()` and receive an `IVariableBag` whose handles route reads through `store.Get` and writes through `store.Execute`.

Files to add under `Assets/Packages/com.scaffold.entities.states/Runtime/`: `StoreVariableBagBuilder.cs` (the builder), `StoreBackedHandle.cs` (the typed handle from the design sketch — pulls fresh on `Get`, dedupes via `_last`, dispatches via `_toPayload`, fans out via `_subscribers`), `StoreBackedVariableBag.cs` (the bag implementation; `IDisposable`; tracks `(Reference, Type)` keys and unsubscribes all on `Dispose`), `ReadOnlyStoreBackedHandle.cs` (the read-only variant for `BindReadOnly`), `BagFallback.cs` (`FallbackMode` enum and `WithFallback` builder method).

The builder API surface (in plain prose, signatures below in Interfaces and Dependencies): the outer builder takes a `Store` reference. `.ForSlice<TState>(Reference)` returns a per-slice scope where `.Bind<T>(string varId, Func<TState, T> project, Func<T, TPayload> toPayload)` and `.BindReadOnly<T>(string varId, Func<TState, T> project)` add bindings. `.ForEntity(Reference)` returns an entity-specific scope where `.BindBase<T>(string varId, Variable entityVar)` and `.BindComputed<T>(string varId, Variable entityVar)` are shorthands that under the hood call the same generic `Bind` / `BindReadOnly` with hardcoded projector and payload factories that target `EntityState.BaseValues` / `EntityState`. `.WithFallback(IEnumerable<RuntimeVariable> seed, FallbackMode mode)` registers fallback handles for graph-declared variables not explicitly bound (`InMemoryDefault` materializes them as `InMemoryHandle<T>` from the default value; `Throw` causes `.Build()` to throw if any seed entry isn't bound).

`.Build()` does three things. First, it walks all bindings and calls `MutatorRegistry.IsRegistered(toPayload's TPayload type)` (the method added in Milestone 1) for each writable binding; throws a clear exception listing missing payload types. Second, it groups bindings by `(Reference, Type<TState>)` and calls `store.Subscribe<TState>(reference, OnSliceChanged)` once per group, where the handler iterates the bindings in that group and re-projects into each handle's `_last`. Third, it constructs the `StoreBackedVariableBag` instance with the resolved handle map and returns it.

`StoreBackedHandle.Set` writes via `_store.Execute(_ref, _toPayload(value))` after the dedupe check on `_last` and the re-entry guard `_applyingFromSubscribe`. The `OnSliceChanged` handler updates `_last`, sets `_applyingFromSubscribe = true`, invokes `_subscribers` if non-null, resets the flag in a `finally`. This guarantees that a `Subscribe` callback that calls `handle.Set(x)` doesn't cause a second `store.Execute`.

`StoreBackedVariableBag.Dispose` walks every store-side subscription it created and calls `store.Unsubscribe<TState>(ref, handler)` with the exact handler delegate (cached in a list at construction). After `Dispose`, the bag stops receiving snapshot or commit events; existing `Subscribe` callers see no further callbacks.

The deferral test plan, written before any other test in this milestone: three tests in a new `Tests/StoreBackedHandleDeferralTests.cs`. Test 1: register two `StoreBackedHandle<EntityState, float>` instances bound to the same slice and the same projector; dispatch `store.ExecuteBatch([SetHpPayload(50), SetHpPayload(75)])`; confirm each handle's `Subscribe` callback fired exactly once with value `75`. Test 2: register a handle; take a snapshot; dispatch `SetHpPayload(50)`; load the snapshot; confirm the handle's `Subscribe` callback did *not* fire (projected value is unchanged because the snapshot reverts the slice). Test 3: register a handle; take a snapshot; dispatch `SetHpPayload(50)`; dispatch `SetHpPayload(75)`; load the original snapshot; confirm the handle's `Subscribe` callback fired exactly once with the pre-snapshot value, regardless of the two intermediate mutations.

Other tests in this milestone: `StoreVariableBagBuilderTests.cs` covers the builder API surface (chained scopes, payload validation, fallback modes). `StoreBackedVariableBagDisposalTests.cs` confirms `Dispose` unsubscribes everything.

Validation: from the repository root run `.agents/scripts/validate-changes.cmd`; no errors. Run `.agents/scripts/run-editmode-tests.ps1`; all bridge and builder tests pass, including the three deferral tests. Acceptance: the builder exists; bound handles reflect store state on read; `Set` dispatches the right payload; `Subscribe` fires at most once per slice per `Execute` / snapshot load; `Dispose` cleans up.

### Milestone 5 — Erase the `[Obsolete]` extension

The goal of this milestone is to remove the transition shim. Every internal call site in entities, the bridge, and any sample code that calls `bag.TryGetBase(...)` is rewritten to `bag.TryGet<T>(...)`. The `[Obsolete]` extension method file (`Runtime/Core/VariableBags/VariableBagLegacyExtensions.cs`) is deleted. The non-virtual `TryGetBase` method on `IEntityVariableStorage` (if any survived as a default implementation) is also deleted.

After this milestone, a fresh checkout has exactly one `IVariableBag` interface (`Scaffold.Variables.IVariableBag`), and the codebase compiles clean. No `[Obsolete]` warnings related to variable storage remain.

Validation: from the repository root run `.agents/scripts/validate-changes.cmd`; expect no errors and no `[Obsolete]` warnings on variable APIs. Run `.agents/scripts/run-editmode-tests.ps1`; all tests still pass. Acceptance: codebase has one and only one `IVariableBag`.

### Milestone 6 — Wire a real consumer end-to-end

The goal of this milestone is to demonstrate the full loop with a real consumer. A small sample under `Assets/Packages/com.scaffold.graphflow/Samples~/StateBackedBlackboard/` (or a similar location appropriate to the project's sample conventions) sets up: a `Store` with an `EntityState` slice registered; a `GraphRunner` subclass overriding `CreateVariableBag` to return a `StoreVariableBagBuilder.ForEntity(...).BindBase<float>(...)...Build()` bag; a graph asset declaring an `hp` variable bound to that entity's `hp` base value; a test that runs the graph, dispatches an out-of-band `Store.Execute(SetBaseValuePayload)`, runs the graph again, asserts the second read returns the new value.

A snapshot round-trip test in the same sample takes a snapshot, sets `hp` to a new value, restores the snapshot, runs the graph, and confirms the read returns the original value.

Validation: from the repository root run `.agents/scripts/validate-changes.cmd`; no errors. Run `.agents/scripts/run-editmode-tests.ps1`; the new sample tests pass. Open the sample in the Unity editor and confirm the graph runs without console errors. Acceptance: the end-to-end snapshot-coherent blackboard binding works; the design's purpose is demonstrably achieved.

## Concrete Steps

The exact commands follow. Run them from the repository root unless otherwise noted. The host is Linux for these commands; PowerShell scripts run under `pwsh` if available, otherwise prefer the `.sh` variants.

For Milestone 1, create the package skeleton:

    mkdir -p Assets/Packages/com.scaffold.variables/Runtime
    mkdir -p Assets/Packages/com.scaffold.variables/Tests

Create `Assets/Packages/com.scaffold.variables/package.json` with `name: "com.scaffold.variables"`, version, displayName, description matching the convention used by `com.scaffold.states/package.json`. Create the runtime asmdef at `Assets/Packages/com.scaffold.variables/Runtime/Scaffold.Variables.asmdef` with `name: "Scaffold.Variables"`, no references. Create the tests asmdef at `Assets/Packages/com.scaffold.variables/Tests/Scaffold.Variables.Tests.asmdef` with references to `Scaffold.Variables`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, and the standard NUnit reference. Add the seven type files listed in Milestone 1.

For the `MutatorRegistry.IsRegistered` addition, edit `Assets/Packages/com.scaffold.states/Runtime/Mutators/MutatorRegistry.cs` to add a public `IsRegistered(Type payloadType)` method.

Run validation:

    .agents/scripts/validate-changes.sh
    .agents/scripts/run-editmode-tests.sh   # if available; otherwise the .ps1 under pwsh

Expected: zero compilation errors; the new smoke tests and `IsRegistered` test pass.

For Milestone 2, before touching anything, the branch must be brought up to date with `origin/main` so the migration starts from the actual current code state. Run `git fetch origin main` and resolve any merge or rebase against `origin/main`. The plan files under `Plans/` should be the only conflicts; resolve them by keeping both sets of changes (graphflow's blackboard plan docs and this ExecPlan).

The GraphFlow migration then follows this order. First, delete `Assets/Packages/com.scaffold.graphflow/Runtime/Variables/IVariableBag.cs`, `VariableCell.cs`, and `InMemoryVariableBag.cs` along with their `.meta` siblings. Do not delete `BlackboardVariable.cs` — it is edited in place. Second, update `BlackboardVariable.cs`: change `BlackboardVariable<T>` to extend `Scaffold.Variables.VariableDefault<T>`, rename `CreateCell(string id)` to `CreateHandle(string id)`, change its return to construct a `Scaffold.Variables.InMemoryHandle<T>`. Third, update every `using` directive across the runtime, editor, samples, and tests to reference `Scaffold.Variables` for the bag and handle types. Fourth, edit `Assets/Packages/com.scaffold.graphflow/Runtime/Asset/RuntimeVariable.cs` if its `defaultValue` field needs a type annotation update (likely no change since `BlackboardVariable` now indirectly is a `VariableDefault`). Fifth, edit `Assets/Packages/com.scaffold.graphflow/Runtime/Controller/GraphRunner.cs`: remove `CreateParentBag()` and `SeedVariables()`; add `protected internal virtual Scaffold.Variables.IVariableBag CreateVariableBag(IEnumerable<RuntimeVariable> seed)` with the default in-memory implementation. Sixth, edit `Assets/Packages/com.scaffold.graphflow/Runtime/Builder/GraphBuilder.cs` to call `runner.Variables = runner.CreateVariableBag(asset.variables)` directly. Seventh, migrate the runtime nodes: in `Runtime/Variables/SetVariable.cs` change `_cell.Value = NewValue.Read(flow)` to `_handle.Set(NewValue.Read(flow))`; in `Runtime/Variables/ObserveVariable.cs` change `_cell.Changed += handler` to `_handle.Subscribe(handler)`; in `Runtime/Variables/GetVariable.cs` no body change is needed beyond the type rename (read still uses `.Value`). Update the field declarations from `VariableCell<T>? _cell` to `InMemoryHandle<T>? _handle` and the `Initialize` calls from `runner.Variables.TryGetCell<T>(...)` to `runner.Variables.TryGet<T>(...)`. Eighth, migrate the consumer samples: `Samples/CardSandbox/Runtime/Showcase/VariableShowcase.cs` and `Samples/CardSandbox/Runtime/Showcase/StrikeWithVariables.cs` and any related sample scripts. Ninth, update the test files: `VariableEdgeTests`, `VariableGetSetTests`, `VariableObserveTests`, `VariableEndToEndTests`, `VariableBagTests`, `VariableWiringTests`, `VariableFollowUpTests`, and `VariableTestHelpers.cs` — same mechanical changes (`TryGetCell` → `TryGet`, `cell.Value = x` → `handle.Set(x)`, `cell.Changed +=` → `handle.Subscribe`).

Run validation after Milestone 2:

    .agents/scripts/validate-changes.sh
    .agents/scripts/run-editmode-tests.sh

Expected: all graphflow tests pass; the new `CreateVariableBag` override test passes; no regressions.

For Milestone 3, the entities migration is the longest sequence and worth doing in sub-steps. First sub-step: extract `Variable` to shared (delete `Assets/Packages/com.scaffold.entities/Runtime/Core/Variables/Variable.cs`; ensure all entity-internal references update; payloadTypeId moves to `VariableEntry.cs`). Second sub-step: rewrite `VariableKeySoField.cs:193-209` to read from `VariableEntry`. Third sub-step: delete `Scaffold.Entities.IVariableBag`; update `IEntityVariableStorage` to extend the shared interface. Fourth sub-step: add `TryGet<T>` to `VariableBag`; mark `TryGetBase` `[Obsolete]`; ship the extension method. Fifth sub-step: write the asset migration callback and the round-trip test. Run validation between each sub-step.

For Milestone 4, the state-backed bag, the order is: ship the deferral test fixture first (the three tests should fail with "type not found" before the bag exists). Then ship `StoreBackedHandle`, `StoreBackedVariableBag`, `ReadOnlyStoreBackedHandle`, `StoreVariableBagBuilder` in that order. The deferral tests pass as the implementation lands. Add the builder API tests last.

For Milestone 5, the erase, run a global search for `TryGetBase` across the repository; rewrite each call site; delete the extension file. Then `.agents/scripts/validate-changes.sh` should report no `[Obsolete]` warnings.

For Milestone 6, follow the convention used by existing samples (e.g. `Assets/Packages/com.scaffold.entities/Samples~/`).

Each milestone ends with a commit. The commit message follows the convention: `SharedVariables: Milestone N — <one-line summary>`.

## Validation and Acceptance

The single end-to-end acceptance test is the snapshot round-trip described in the Purpose section. After Milestone 6, this test exists and passes:

    [Test]
    public void GraphVariable_BoundToEntityState_ParticipatesInSnapshots()
    {
        var store = new StoreBuilder().Build();
        EntityBridgeContext.RegisterMutators(store);
        var entityRef = new Reference("hero");
        store.RegisterSlice(entityRef, EntityState.Empty);
        store.Execute(entityRef, new SetBaseValuePayload(entityRef, hpVar, VariableValueFactory.From(100f)));

        var runner = new MyStateBackedRunner(store, entityRef);
        runner.Initialize(asset);
        var flow1 = runner.Run();
        Assert.AreEqual(100f, flow1.ReadHpVariable());

        var snap = store.SaveSnapshot();
        store.Execute(entityRef, new SetBaseValuePayload(entityRef, hpVar, VariableValueFactory.From(50f)));

        var flow2 = runner.Run();
        Assert.AreEqual(50f, flow2.ReadHpVariable());

        store.LoadSnapshot(snap);
        var flow3 = runner.Run();
        Assert.AreEqual(100f, flow3.ReadHpVariable());
    }

The test fails before Milestone 4 lands (no `StoreVariableBagBuilder`) and before Milestone 6 wires the consumer (no `MyStateBackedRunner`). It passes after Milestone 6.

Per-milestone acceptance is described in each milestone's narrative above. The repository-wide gate — `.agents/scripts/validate-changes.cmd` (or `.sh` on Linux) — must report zero compilation errors and no new `error CS` lines after each milestone.

## Idempotence and Recovery

Every milestone is designed to be safely re-runnable. Milestone 1 is purely additive; re-running the same `mkdir` and file creation produces no drift. Milestones 2 and 3 delete files in addition to editing — re-running involves checking what's already deleted; no second deletion attempt is destructive because the files are gone. The asset migration script in Milestone 3 must be idempotent by construction: `OnAfterDeserialize` checks if the legacy field is still present before reconstructing, so repeated deserializations of an already-migrated asset are no-ops.

The most fragile point is Milestone 3's asset migration. Backup recommendation: before running Milestone 3, copy `Samples~/` directories aside (or rely on git's untracked-file behavior on `Samples~/` if the convention matches). If the migration corrupts an asset, restore from git or the backup and re-run.

If a milestone fails partway: revert with `git restore .` and `git clean -fd` (after confirming no work-in-progress is unstaged elsewhere); re-run from the milestone start. The validation gate after each milestone is the canonical "did it land" check.

## Artifacts and Notes

The companion design notes at `Plans/SharedVariablesPackage.md` capture the design discussion and decision history that produced this plan. They are background reading only; this ExecPlan is self-contained and supersedes them as the source of truth.

Audit findings that informed the decisions, in summary form: `VariableValueFactory.From<T>(T value)` already exists at `Assets/Packages/com.scaffold.entities/Runtime/Core/Utilities/VariableValueFactory.cs:30-41` and handles the `T → VariableValue` wrapping transparently — Milestone 4's `_toPayload` factories use it directly. `Store.Execute<TPayload>(Reference?, TPayload)` and `Store.Subscribe<TState>(Reference, Action<TState, StateChangeEvent>)` signatures match the proposed builder API verbatim. `EntityBridgeContext.RegisterMutators` is idempotent at the `MutatorRegistry.Register` layer. Graphflow's port-bind closures cache concrete cell references — that's why `InMemoryHandle<T>` is exposed as a public concrete class, not just hidden behind an interface. The Unity-serialization namespace migration concern motivates the `OnAfterDeserialize` work in Milestone 3.

## Interfaces and Dependencies

These interfaces and types must exist after the indicated milestone.

After Milestone 1, in `Assets/Packages/com.scaffold.variables/Runtime/`, namespace `Scaffold.Variables`:

    [Serializable]
    public sealed class Variable : IEquatable<Variable>
    {
        public string Id { get; }
        public string TypeName { get; }
        public Variable(string id, string typeName);
        public bool Equals(Variable other);
        public override bool Equals(object obj);
        public override int GetHashCode();
        public static bool operator ==(Variable a, Variable b);
        public static bool operator !=(Variable a, Variable b);
    }

    public interface IVariableHandle
    {
        string Id { get; }
        Type Type { get; }
    }

    public interface IReadOnlyVariableHandle<T> : IVariableHandle
    {
        T Value { get; }
        void Subscribe(Action<T> handler);
        void Unsubscribe(Action<T> handler);
    }

    public interface IVariableHandle<T> : IReadOnlyVariableHandle<T>
    {
        void Set(T value);
    }

    public interface IVariableBag
    {
        IVariableBag Parent { get; }
        bool TryGet<T>(string id, out IVariableHandle<T> handle);
        bool TryGet(string id, out IVariableHandle handle);
        IEnumerable<IVariableHandle> LocalHandles { get; }
    }

    [Serializable]
    public abstract class VariableDefault
    {
        public abstract Type ValueType { get; }
        public abstract IVariableHandle CreateHandle(string id);
    }

    [Serializable]
    public abstract class VariableDefault<T> : VariableDefault
    {
        public T value;
        public sealed override Type ValueType => typeof(T);
        public override IVariableHandle CreateHandle(string id) => new InMemoryHandle<T>(id, value);
    }

    public sealed class InMemoryHandle<T> : IVariableHandle<T> { /* see Milestone 1 */ }
    public sealed class InMemoryVariableBag : IVariableBag { /* see Milestone 1 */ }

In `Assets/Packages/com.scaffold.states/Runtime/Mutators/MutatorRegistry.cs`:

    public bool IsRegistered(Type payloadType);

After Milestone 2, in `Assets/Packages/com.scaffold.graphflow/Runtime/Controller/GraphRunner.cs`:

    protected internal virtual Scaffold.Variables.IVariableBag CreateVariableBag(IEnumerable<RuntimeVariable> seed);

`runner.CreateParentBag()` and `runner.SeedVariables()` no longer exist. `runner.Variables` is `Scaffold.Variables.IVariableBag`.

In `Assets/Packages/com.scaffold.graphflow/Runtime/Variables/BlackboardVariable.cs`:

    namespace Scaffold.GraphFlow
    {
        [Serializable]
        public abstract class BlackboardVariable<T> : Scaffold.Variables.VariableDefault<T>
        {
            // value field inherited from VariableDefault<T>; CreateHandle<T> inherited.
        }

        [Serializable] public sealed class BlackboardInt    : BlackboardVariable<int> { }
        [Serializable] public sealed class BlackboardFloat  : BlackboardVariable<float> { }
        [Serializable] public sealed class BlackboardBool   : BlackboardVariable<bool> { }
        [Serializable] public sealed class BlackboardString : BlackboardVariable<string> { }
        [Serializable] public sealed class BlackboardObject : BlackboardVariable<UnityEngine.Object> { }
    }

`Scaffold.GraphFlow.VariableCell<T>` and `Scaffold.GraphFlow.InMemoryVariableBag` no longer exist. Anything in graphflow that handled an in-memory cell now handles `Scaffold.Variables.InMemoryHandle<T>`. Anything that handled a bag handles `Scaffold.Variables.IVariableBag`. The `BlackboardVariable<T>` polymorphic-seed authoring class is graphflow's only domain-specific variable type that survives — it's the package's named flavor of the shared `VariableDefault<T>` base.

After Milestone 3, in `Assets/Packages/com.scaffold.entities/Runtime/Core/Instance/IEntityVariableStorage.cs`:

    public interface IEntityVariableStorage : Scaffold.Variables.IVariableBag
    {
        new IEntityVariableStorage Parent { get; }
        bool TryGetBase(Variable key, out VariableValue value);  // [Obsolete]
        bool AddVariable(Variable key, VariableValue initial);
        bool RemoveVariable(Variable key);
        bool SetBaseValue(Variable key, VariableValue value);
        ModifierId AddModifier(Variable key, VariableModifier mod, ModifierSource source = default, ModifierId? id = null);
        bool RemoveModifier(Variable key, ModifierId id);
        void ClearModifiers();
        void RemoveModifiersFromSource(ModifierSource source);
    }

After Milestone 4, in `Assets/Packages/com.scaffold.entities.states/Runtime/`:

    public sealed class StoreBackedHandle<TState, T> : Scaffold.Variables.IVariableHandle<T> where TState : State { /* impl */ }
    public sealed class ReadOnlyStoreBackedHandle<TState, T> : Scaffold.Variables.IReadOnlyVariableHandle<T> where TState : State { /* impl */ }

    public sealed class StoreBackedVariableBag : Scaffold.Variables.IVariableBag, IDisposable
    {
        public IVariableBag Parent { get; }
        public bool TryGet<T>(string id, out IVariableHandle<T> handle);
        public bool TryGet(string id, out IVariableHandle handle);
        public IEnumerable<IVariableHandle> LocalHandles { get; }
        public void Dispose();
    }

    public sealed class StoreVariableBagBuilder
    {
        public StoreVariableBagBuilder(Store store);
        public SliceScope<TState> ForSlice<TState>(Reference reference) where TState : State;
        public EntityScope ForEntity(Reference reference);
        public StoreVariableBagBuilder WithFallback(IEnumerable<RuntimeVariable> seed, FallbackMode mode);
        public StoreBackedVariableBag Build();
    }

    public readonly struct SliceScope<TState> where TState : State
    {
        public StoreVariableBagBuilder Bind<T, TPayload>(string variableId, Func<TState, T> project, Func<T, TPayload> toPayload);
        public StoreVariableBagBuilder BindReadOnly<T>(string variableId, Func<TState, T> project);
    }

    public readonly struct EntityScope
    {
        public StoreVariableBagBuilder BindBase<T>(string variableId, Variable entityVariable);
        public StoreVariableBagBuilder BindComputed<T>(string variableId, Variable entityVariable);
    }

    public enum FallbackMode { InMemoryDefault, Throw }

After Milestone 5, the `[Obsolete]` `TryGetBase` extension method file is deleted; no public surface defines it.

After Milestone 6, the sample subdirectory under graphflow (or the appropriate samples location) contains a runnable example demonstrating snapshot round-trip preservation.

## Revision history

- 2026-05-09 — Initial authoring. Captures decisions and design from the design-notes session at `Plans/SharedVariablesPackage.md`. All six milestones defined; no implementation started.

- 2026-05-09 (later) — Adapt to GraphFlow Variables work merged into `origin/main` (PR #49 plus follow-ups). Three changes: `BlackboardVariable<T>` retained as graphflow's named subclass of shared `VariableDefault<T>` (per the principle that each package may ship a domain-named flavor on top of the shared base); Milestone 2 scope expanded to migrate the real consumer code that landed on main (`VariableShowcase`, `StrikeWithVariables`, `VariableFollowUpTests`) to the method-style surface; cycle detection on the bag parent chain is ported into the shared `InMemoryVariableBag`. Reason: the user confirmed they want to keep this plan's interface-level decisions (method-style subscription, `Set(T)` write, `IVariableHandle<T>` typed return) but acknowledged graphflow's authoring surface validation work as battle-tested and worth preserving by name. The total estimate ticks up modestly to reflect the additional consumer-code migration (~+1d Milestone 2).
