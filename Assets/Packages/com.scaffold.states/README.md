# Scaffold States

Runtime slice/store pattern for immutable game state. **`Store`** composes **`Slice`** instances (each binds an **`IReference`** to a **`State`** record), applies **`Mutator<TState>`** updates atomically, fires typed change notifications, and supports snapshot save/load for time travel and persistence.

The shape is Redux-flavored: state lives in records, mutations are pure functions, derived state is computed via aggregate selectors. The differences from stock Redux are deliberate: state is read directly via `Get<T>` rather than through a polled selector chain (push, not pull), and aggregates are cached so reads are O(1).

## Dependencies

- **`Scaffold.Maps`**: typed `Map<TPrimary, TSecondary, TValue>` used by `Store` to index slices by `(IReference, Type)`.
- **`Scaffold.Records`**: required transitively by Maps.
- **`Scaffold.Pooling`**: `Pool<T>` used for the mutator runner.

## Layout

- **`Runtime/Store.cs`** — central API: `Execute`, `Get`, `TryGet`, `Subscribe`, `RegisterSlice`, `RegisterAggregate`, `SaveSnapshot`, `LoadSnapshot`.
- **`Runtime/State/`** — `State`, `AggregateState`, `Slice`, `AggregateSlice`, `Snapshot`, `StateChangeEvent`, `IAggregateProvider`, `IAggregateRebuild`.
- **`Runtime/Builders/`** — `StoreBuilder` and per-state builders for composition.
- **`Runtime/Events/`** — `StateEventHandler`, `DeferredStateEventHandler`, `Ledger`, `TypedSubscription`.
- **`Runtime/Mutators/Mutator.cs`** — `Mutator<TState>` and `Mutator<TState, TPayload>`.
- **`Runtime/Pipeline/`** — `MutatorRegistry`, `MutatorRunner`, `IStoreScratchpad`.
- **`Samples/`** — small store demos plus the comprehensive **`Samples/CardGame/`** sample showing per-key slices, aggregates, foreign-key joins, and the visibility / hidden-information pattern.

## Concepts

**`IReference`** is a marker interface for entity identity. Anything that implements it can be a slice key. Use `record` types so equality is value-based and stable across sessions:

    public sealed record PlayerId(int Value) : IReference;
    public sealed record CardId(int Value) : IReference;

**`State`** is an immutable record. The store stores one `State` per `(IReference, Type)`. To "mutate" a slice, a `Mutator` returns a new record; the store replaces the previous instance and fires `Notify`.

    public sealed record CounterState(int Value) : State;

**`Slice`** wraps a `State` plus its `IReference`. Slices are the canonical unit of storage. They live in `Store.map`.

**`AggregateState`** is a derived `State` produced by an `IAggregateProvider`. It rebuilds in response to changes in its declared dependencies. It cannot be mutated directly. See [When to use an aggregate](#when-to-use-an-aggregate).

**`Mutator<TState>`** and **`Mutator<TState, TPayload>`** are pure functions: `(state, payload, scope) -> newState`. Multiple mutators can register against the same payload type — one `Execute` runs them all in one transactional commit.

**`Reference.Null`** is the singleton "global" reference. Use it when an aspect of state has no natural per-entity key (a global game phase, a single shared counter).

## Quick start

Build a store, register a mutator, dispatch a payload, observe change events.

    public sealed record CounterState(int Value) : State;
    public sealed record IncrementPayload(int Delta);

    public sealed class IncrementCounter : Mutator<CounterState, IncrementPayload>
    {
        public override CounterState Change(CounterState state, IncrementPayload payload, IStateScope scope)
            => new CounterState(state.Value + payload.Delta);
    }

    var builder = new StoreBuilder();
    builder.AddState(new CounterState(0));
    builder.RegisterMutator(new IncrementCounter());
    Store store = builder.Build();

    store.Subscribe<CounterState>((_, s, evt) => Debug.Log($"{evt}: {s.Value}"));
    store.Execute(new IncrementPayload(5));   // logs "Updated: 5"
    int now = store.Get<CounterState>().Value; // 5

For a richer example with per-entity slices, aggregates, foreign-key lists, and visibility flips, read `Samples/CardGame/CardGameStoreFactory.cs` end-to-end.

## How to model slices

Slices are keyed by `(IReference, Type)`. Two design rules fall out of that:

**A reference identifies one entity. A state type identifies one aspect of that entity.** A `PlayerId` may have a `PlayerCoreState` (name, health), a `PlayerHandState` (which card ids are in hand), a `PlayerStatsState` (atk, def). Each is a separate slice on the same key. Splitting aspects is cheap and gives subscribers the finest possible granularity — a hand change does not wake stat-watchers.

**Slice presence carries meaning.** A slice that is registered means the aspect is "live" for that entity from this client's point of view. A slice that is not registered means absence. Use this for visibility and lifecycle:

- A card you have not yet learned exists: no slices for that `CardId`.
- A card you know exists but cannot identify (face-down): `CardRuntimeState` registered, `CardKnowledgeState` not.
- A card you have full information for: both registered.

`Reveal` and `Conceal` then become `RegisterSlice` and `UnregisterSlice` calls. Subscribers get `Created` / `Removed` events for free.

**Container slices hold foreign keys, not snapshots.** A `HandState` should hold `IReadOnlyList<CardId>`, never `IReadOnlyList<CardData>` and never `IReadOnlyList<CardAggregate>`. The card lives once in the store under its `CardId`. Anywhere that needs to refer to it holds the id. This is exactly Redux's normalized-state pattern; the store does the `byId` job, foreign-key lists do the membership job.

**Authoring data is not state.** Card definitions (name, base stats, art, effect script) are immutable across a build. They belong in a catalog object outside the store, looked up by a definition id. The store carries the per-instance link `CardId -> CardDefId` (often as part of `CardKnowledgeState`), not the definition itself. Putting definitions in slices makes them mutable, makes them part of snapshot save/load (wasteful), and conflates per-run state with per-build content.

**Per-key slices, not one big map slice.** If you have many cards, prefer N small slices keyed by `CardId` over one `CardCatalogState(IReadOnlyDictionary<CardId, CardData>)`. Per-key gives per-card subscription granularity, register/unregister is the right semantic for visibility, and mutating one card does not invalidate every reader of the catalog. The single-map shape is fine when N is small (a handful of zones, fixed game phases) but does not scale.

## When to use an aggregate

An aggregate is the right tool when **all three** of the following are true:

1. The value is **derived** from other slices. If you can serve it from a single canonical slice, it is not an aggregate; it is just a slice.
2. The derivation is **non-trivial enough to cache**. A `string Name => coreState.Name` is not worth an aggregate.
3. The output is **read more than the inputs change**. Aggregates trade build cost for read cost. If the inputs change every frame and nothing reads the result, you are paying for nothing.

Three valid use cases:

- **Cross-slice join.** `PlayerHandView` joins `HandState` (list of ids) with each card's per-card slices and the catalog. Callers read one tidy struct instead of orchestrating three Gets.
- **Derived scalar.** `PlayerView.TotalAttack` summed from base stats, gear bonuses, and active modifiers. Recomputes when any input changes; readers get a number.
- **Memoized selector.** A computation that several subscribers want, where running it once per change is cheaper than running it once per read.

Aggregates can depend on other aggregates. Reading `scope.Get<OtherAggregate>(ref)` inside `BuildCore` is supported and registers a transitive dependency through the standard subscription path.

**When NOT to use an aggregate.** Do not build a single mega-aggregate that exposes every field anyone might want. Make several small aggregates per UI concern (`CardStatsAggregate`, `CardArtAggregate`, etc.) so leaf views subscribe to the smallest thing they actually render. Do not put `IReadOnlyList<CardAggregate>` inside a higher aggregate; pass `IReadOnlyList<CardId>` and let the leaf views read each card's aggregate independently.

See `Plans/AggregateRebuildOptimization/AggregateRebuildOptimization-ExecPlan.md` for the roadmap on output-equality short-circuit, commit-batch coalescing, and auto-tracked dependencies that will reduce the cost of nested aggregates.

## Patterns

These shapes show up across the samples and are the recommended defaults.

**Per-aspect slice split.** One reference, many state types, each focused on one concern.

    builder.AddState(playerId, new PlayerCoreState("You", 30, 30));
    builder.AddState(playerId, new PlayerHandState(Array.Empty<CardId>()));
    builder.AddState(playerId, new PlayerDeckState(deckOrder));

**Identity as a reference, definition as a catalog entry.** `CardId` for the instance, `CardDefId` for the recipe, `CardCatalog` for the table of recipes (outside the store).

    public sealed record CardId(int Value) : IReference;
    public sealed record CardDefId(int Value);
    public sealed record CardKnowledgeState(CardDefId Def) : State;
    public sealed class CardCatalog { /* CardDefId -> CardDef lookup */ }

**Mutator pair sharing a payload.** A multi-slice operation registers one mutator per affected slice type; one `Execute` runs them all in one commit.

    public sealed record DrawCardPayload(PlayerId Player, CardId Card) : IPayloadReference
    {
        public IReference GetReference() => Player;
    }

    public sealed class DrawCard_DeckMutator : Mutator<DeckState, DrawCardPayload> { ... }
    public sealed class DrawCard_HandMutator : Mutator<HandState, DrawCardPayload> { ... }

    builder.RegisterMutator(new DrawCard_DeckMutator());
    builder.RegisterMutator(new DrawCard_HandMutator());
    store.Execute(new DrawCardPayload(player, topCardId));

**Service layer wraps multi-slice operations.** A static helper reads pre-payload state, builds the payload, dispatches. Mutators stay pure and order-independent.

    public static CardId? DrawTop(Store store, PlayerId player)
    {
        var deck = store.Get<DeckState>(player);
        if (deck.Order.Count == 0) return null;
        var top = deck.Order[0];
        store.Execute(new DrawCardPayload(player, top));
        return top;
    }

**Visibility via registration.** Reveal and conceal as slice register/unregister. Aggregates rebuild via their existing subscriptions.

    store.RegisterSlice(card, new CardKnowledgeState(def));   // reveal
    store.UnregisterSlice<CardKnowledgeState>(card);           // conceal

Inside a `BuildCore` that may see absent slices, use `TryGet`:

    if (!scope.TryGet<CardKnowledgeState>(id, out var knowledge))
    {
        return CardView.FaceDown(id);
    }

**Aggregates over scalars, IDs over snapshots.** A top-level player view exposes scalars and id lists; per-card detail is a separate aggregate read by the leaf UI.

    public sealed record PlayerView(
        string Name, int Hp, int Mana,
        int HandSize, int DeckSize,
        IReadOnlyList<CardId> HandCardIds) : AggregateState;

## Antipatterns

These shapes look reasonable in isolation but cause real pain at scale. Each is paired with the better alternative.

**Mega-aggregate.** One aggregate that consolidates every field of every dependency on an entity. Any change to any input rebuilds and re-renders everything. Better: per-concern aggregates so leaf views subscribe to the smallest thing they need.

**Aggregate as a long-lived handle.** Caching a `CardAggregate` instance in a UI script and reading from it later. Aggregates are snapshots; the cached value goes stale the moment the underlying slice changes. Better: hold the `CardId` and call `store.Get<CardAggregate>(id)` at render time. Reads are O(1); the result is always live.

**Authoring data in slices.** Putting card definitions, prefab references, or tuning constants into the store because it is the only system with structure. They become snapshot-saved, mutable, and entangled with per-run state. Better: a static catalog or `ScriptableObject` registry outside the store, looked up by id.

**Container slices that hold state objects, not ids.** `HandState(IReadOnlyList<CardSnapshot>)`. Now there are two sources of truth: the cards in their own slices, and the cached snapshots in the hand. They drift. Better: foreign keys (`IReadOnlyList<CardId>`); the aggregate joins on read.

**One object as both `IReference` and `AggregateState`.** Tempting because it lets you "pass the card around." Reference identity must be stable; aggregate values change every rebuild. Conflating them breaks dictionary equality, breaks subscription matching, and breaks visibility (you cannot model "I have the id but not the definition" because the id IS the definition). Better: a small `CardId` record for the key, a separate `CardView` aggregate for the value.

**`try/catch (KeyNotFoundException)` for absent slices.** `Get<T>` throws on miss; catching the throw on a hot path (every aggregate rebuild) allocates exception objects and unwinds the stack. Better: `IStateScope.TryGet<T>(reference, out var state)`.

**`SubscribeAllReferences<T>` in production code.** Fires every time **any** reference's `T` slice changes — wakes consumers that do not care. Acceptable in samples or when the count is small. Better: subscribe to specific `(reference, type)` pairs. The `AutoTrackedAggregateProvider` planned in the optimization plan will derive these subscriptions automatically from `BuildCore` reads.

**Mutators that read other slices the mutators are also writing.** Reading `scope.Get<HandState>(...)` inside a mutator that runs alongside another mutator writing `HandState` exposes overlay-vs-canonical ordering. Order of registration becomes load-bearing. Better: precompute everything at the dispatch site (the service layer), pass it in the payload, and have each mutator read only its own slice.

**Same-payload mutators that "validate" against shared state.** `DiscardFromHand_CounterMutator` checking "is this card actually in hand" by reading `HandState`. If the hand mutator already removed the card, the counter sees it as absent. Better: validate once at the dispatch site (the service layer); dumb mutators that trust the payload.

**Aggregate that mutates state.** Calling a method that triggers `Execute` from inside `BuildCore`. The store has no API path for this, but composing `Wire` callbacks that produce side effects can recreate it. Treat `BuildCore` as a pure function of scope; if you need to react to an aggregate change, subscribe via `Subscribe<TAggregate>(...)` from outside.

## Use cases by sample

**`Samples/SampleStoreFactory.CreateFullDemo`** — global counter and notes slices plus a `TotalsDashboardState` aggregate. The simplest case: one reference (`Reference.Null`), one aggregate that joins two slices. Use this shape when state is genuinely global to the session.

**`Samples/SampleStoreFactory.CreateKeyedCounterDemo`** — per-key counters with a routing payload (`RoutedCounterPayload : IPayloadReference`). Shows how to use a payload's `GetReference()` so a single registered mutator handles many keys.

**`Samples/CardGame/CardGameStoreFactory.CreateDefaultDemo`** — full card-game shape:

- Per-card slices keyed by `CardId` (`CardRuntimeState`, `CardKnowledgeState`).
- Per-player slices keyed by `PlayerId` (`PlayerCoreState`, `HandState`, `DeckState`, `DiscardState`, `DiscardCounterState`).
- A catalog (`CardCatalog`) outside the store.
- Per-card aggregate (`CardView`) joining knowledge + runtime + catalog.
- Per-zone aggregates (`HandView`, `DeckView`, `DiscardView`) joining the zone slice with per-card views.
- A top-level `PlayerView` with scalars and id lists.
- Multi-slice payloads (`DrawCardPayload`, `DiscardFromHandPayload`) with one mutator per affected slice.
- `Reveal` / `Conceal` via direct register / unregister.

Read the providers and the factory together to see how the patterns above compose into a working game shape.

## Subscriptions

`Subscribe`, `SubscribeAllReferences`, and `SubscribeAny` take **`Action<IReference, TState, StateChangeEvent>`** so callbacks can distinguish:

- **`Created`** — `RegisterSlice` or `RegisterAggregate` (post-build).
- **`Updated`** — mutator commit, snapshot apply, aggregate rebuild.
- **`Removed`** — `UnregisterSlice` or snapshot prune.

For keyed subscriptions, **`Unsubscribe<TState>(IReference, Action<...>)`** removes the same delegate instance that was passed to `Subscribe`.

Aggregates emit `Updated` events through the same path. Subscribe to an aggregate exactly like a canonical slice:

    store.Subscribe<HandView>(playerId, (_, hand, _) => RebuildHandUi(hand));

## Snapshots

- **`SaveSnapshot()`** copies every canonical slice row from the store into a new `Snapshot`. Aggregates are not stored; they are derived from canonical state and rebuilt automatically after `LoadSnapshot`.
- **`LoadSnapshot(snapshot)`** performs a full restore for canonical rows: it applies every snapshot entry, then removes any canonical slice whose `(reference, state type)` is not in the snapshot. Runtime-registered slices that did not exist when the snapshot was saved disappear cleanly on rollback.
- Mutator commits (`Execute`, batch payloads) use an internal merge-only path: they apply pending changes to existing slices without pruning missing rows. A single mutation cannot strip unrelated slices.

## Deferred event dispatch

When many commits in one logical frame would each fire `Notify` (cascading into redundant UI work), wrap the default handler with **`DeferredStateEventHandler`**. It forwards `Subscribe*` calls to an inner handler and buffers `Notify` while a defer scope is active (`BeginDeferScope()`). Call **`Flush()`** to deliver pending notifications.

**`StateEventMergeMode`**

- **`PreserveAll`** — replay every buffered notification in order.
- **`LatestPerKey`** — at most one inner notification per `(reference, state type)`; the last state wins. Order follows first-seen keys.

Composition example:

    var inner = StateEventHandlers.CreateDefault();
    var deferred = new DeferredStateEventHandler(inner, StateEventMergeMode.LatestPerKey);
    builder.AddEventHandler(deferred);
    using (deferred.BeginDeferScope())
    {
        // mutations that would normally notify many times
    }
    deferred.Flush();

Control surface: **`IStateEventDeferralController`** (`Flush`, `BeginDeferScope`). Cast `store.Events` to that interface when the store was built with the decorator, or keep a direct reference to the `DeferredStateEventHandler` instance.

For view-layer batching of binding refresh (separate concern), see MVVM `BindingUpdateTiming` / deferred binding in `com.scaffold.mvvm` and `Plans/BindingDeferredUpdate/BindingDeferredUpdate-ExecPlan.md`.

## Roadmap

Performance and ergonomics improvements are tracked in `Plans/AggregateRebuildOptimization/AggregateRebuildOptimization-ExecPlan.md`. Highlights:

- **Output equality short-circuit** — aggregates skip propagation when the rebuilt value equals the cached value.
- **Commit-batch coalescing** — multi-slice payloads cause one rebuild per affected aggregate at the end of the commit, not one per dependency notification.
- **Auto-tracked dependencies** — replace manual `Wire(...)` declarations with subscriptions discovered from `BuildCore` reads.
- **Aggregate unregister** — lifetime API for transient aggregates with handler cleanup.

Milestone 1 (`IStateScope.TryGet<T>`) has shipped.
