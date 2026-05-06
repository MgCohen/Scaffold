# Scaffold States

Runtime slice/store pattern for immutable game state. **`Store`** composes **`Slice`** instances, applies **`Mutator`** updates atomically, fires typed change notifications, and supports snapshot save/load.

## Dependencies

- **`Scaffold.Maps`** — typed map indexing slices by `(IReference, Type)`.
- **`Scaffold.Records`** — required by Maps.
- **`Scaffold.Pooling`** — pool used by the mutator runner.

## Layout

- `Runtime/Store.cs` — central API (`Execute`, `Get`, `Subscribe`, snapshots).
- `Runtime/State/` — `State`, `AggregateState`, `Slice`, `AggregateSlice`, `Snapshot`.
- `Runtime/Builders/` — `StoreBuilder` and per-state builders.
- `Runtime/Events/` — `StateEventHandler`, `DeferredStateEventHandler`, `StateEventHandlerFactory`, `TypedSubscription`, `Ledger`.
- `Runtime/Mutators/` — `Mutator<TState>` and `Mutator<TState, TPayload>`.
- `Runtime/Pipeline/` — `MutatorRegistry`, `MutatorRunner`.
- `Samples~/` — small demos plus `Samples~/CardGame/` for the full pattern (Package Manager-only via `~` suffix).

## Concepts

| Term | Role | Shape |
|---|---|---|
| `IReference` | Marker for entity identity | `record PlayerId(int Value) : IReference;` |
| `State` | Immutable record stored per `(reference, type)` | `record CounterState(int Value) : State;` |
| `Slice` | Canonical row holding one `State` at one reference | created via `store.RegisterSlice(...)` |
| `AggregateState` | Derived state, rebuilt from declared deps | `record TotalsView(int Sum) : AggregateState;` |
| `Mutator<TState, TPayload>` | Pure `(state, payload, scope) → newState` | `class Inc : Mutator<CounterState, IncPayload>` |
| `Reference.Null` | Singleton "global" key | default for the `Get<T>()` overload |

## Basic API

### Build a store

Compose slices, aggregates, and mutators via `StoreBuilder`.

    var builder = new StoreBuilder();
    builder.AddState(new CounterState(0));
    Store store = builder.Build();

### Register a slice

Add a canonical slice at a reference with an initial state.

    var key = new SampleKey("A");
    store.RegisterSlice(key, new CounterState(0));

### Register a mutator

Bind a `Mutator<TState, TPayload>` so dispatching the payload runs it.

    builder.RegisterMutator(new IncrementCounter());
    // or, post-build:
    store.RegisterMutator(new IncrementCounter());

### Mutate a slice

Dispatch a payload; bound mutators run and commit atomically.

    store.Execute(new IncrementPayload(5));

### Read a slice

`Get` throws on miss; `TryGet` is the absent-tolerant read.

    int value = store.Get<CounterState>(key).Value;
    if (store.TryGet<CounterState>(key, out var s)) { /* present */ }

### Subscribe to changes

Receive `Created`, `Updated`, and `Removed` events for one slice type.

    store.Subscribe<CounterState>(key, (_, s, evt) => Debug.Log($"{evt}: {s.Value}"));

### Unregister a slice

Remove a canonical slice; subscribers receive a `Removed` event.

    store.UnregisterSlice<CounterState>(key);

## More

### Aggregates

Derived state computed by an `AggregateProvider`; cached, auto-rebuilt on declared deps, read like any slice.

    public sealed class TotalsProvider : AggregateProvider<TotalsView>
    {
        public override void Wire(IStoreScope scope, IAggregateRebuild rebuild)
            => scope.Events.SubscribeAllReferences<CounterState>((_, _, _) => rebuild.RequestRebuild());

        protected override TotalsView BuildCore(IStateScope scope)
            => new TotalsView(scope.GetAll<CounterState>().Sum(c => c.Value));
    }

    builder.RegisterAggregate(new TotalsProvider());
    int total = store.Get<TotalsView>().Sum;

### Multi-slice mutations

One payload, N mutators; one `Execute` runs them all in one commit.

    public sealed record DrawCardPayload(PlayerId Player, CardId Card) : IPayloadReference
    {
        public IReference GetReference() => Player;
    }

    builder.RegisterMutator(new DrawCard_DeckMutator());
    builder.RegisterMutator(new DrawCard_HandMutator());
    store.Execute(new DrawCardPayload(player, topCard));

### Snapshots

Capture all canonical slices; restoring prunes anything missing from the snapshot. Aggregates rebuild automatically.

    Snapshot snap = store.SaveSnapshot();
    store.LoadSnapshot(snap);

### Deferred event dispatch

Buffer `Notify` calls during a scope; flush as a batch (`PreserveAll` or `LatestPerKey`).

    var inner = StateEventHandlerFactory.CreateDefault();
    var deferred = new DeferredStateEventHandler(inner, StateEventMergeMode.LatestPerKey);
    builder.AddEventHandler(deferred);

    using (deferred.BeginDeferScope())
    {
        // many mutations
    }
    deferred.Flush();

## Flows

### Single-slice mutation

1. `store.Execute(payload)` rents a `MutatorRunner` from the pool.
2. The bound `Mutator.Change` runs against the current state, writes to the overlay.
3. `CommitOverlay → ApplySnapshot` calls `Set` on the slice.
4. `Set → Notify(ref, state, Updated)` fires subscribers.

### Multi-slice payload

1. `store.Execute(payload)` rents one runner.
2. Each bound mutator reads its slice via `IStateScope`, writes to its slice in the overlay.
3. `CommitOverlay` iterates overlay entries; each `Set` fires its own `Notify`.
4. Aggregates subscribed to those slices `RequestRebuild`, recompute, and emit their own `Updated`.

> Today, an aggregate that depends on N changed slices rebuilds N times in one commit. `Plans/AggregateRebuildOptimization` Milestone 4 collapses that to once per commit.

### Reveal (presence flip)

1. `store.RegisterSlice(card, new CardKnowledgeState(def))` adds the row.
2. `Notify(card, state, Created)` fires.
3. Aggregates subscribed to `(card, CardKnowledgeState)` rebuild — `IsKnown` flips to `true`.
4. Downstream aggregates that read that card's view rebuild in turn.

## Do / Don't

### Container slices hold ids, not snapshots

    // DON'T — two sources of truth, drifts under mutation
    public sealed record HandState(IReadOnlyList<CardAggregate> Cards) : State;

    // DO — foreign keys; aggregates join on read
    public sealed record HandState(IReadOnlyList<CardId> Cards) : State;

### Identity, definition, and aggregate are three different things

    // DON'T — one type tries to be all three
    public sealed record Card(int Id, string Name, int Atk) : IReference, AggregateState;

    // DO — split them
    public sealed record CardId(int Value) : IReference;            // identity
    public sealed record CardDef(int Cost, int Atk, int Hp);        // definition (outside the store)
    public sealed record CardView(/* fields */) : AggregateState;   // derived, in the store

### Per-concern aggregates, not mega-aggregates

    // DON'T — any input change rebuilds everything subscribed
    public sealed record EverythingPlayerView(/* 20 fields */) : AggregateState;

    // DO — leaf views subscribe to the smallest thing they render
    public sealed record PlayerStatsView(string Name, int Hp) : AggregateState;
    public sealed record PlayerHandView(IReadOnlyList<HandSlot> Slots) : AggregateState;

### `TryGet`, not `try/catch (KeyNotFoundException)`

    // DON'T — exception unwinding on a hot path
    try { return scope.Get<CardKnowledgeState>(id); }
    catch (KeyNotFoundException) { return null; }

    // DO
    if (scope.TryGet<CardKnowledgeState>(id, out var k)) { /* ... */ }

### Validate at the dispatch site, not inside co-mutating mutators

    // DON'T — reads HandState that another mutator on the same payload writes;
    //         result depends on registration order
    public sealed class Discard_CounterMutator : Mutator<DiscardCounterState, DiscardPayload>
    {
        public override DiscardCounterState Change(DiscardCounterState s, DiscardPayload p, IStateScope scope)
            => scope.Get<HandState>(p.Player).Cards.Contains(p.Card) ? new(s.Count + 1) : s;
    }

    // DO — service layer validates, mutators trust the payload
    public static bool DiscardFromHand(Store s, PlayerId p, CardId c)
    {
        if (!s.Get<HandState>(p).Cards.Contains(c)) return false;
        s.Execute(new DiscardPayload(p, c));
        return true;
    }

### Per-key slices over a single map slice (when N is large)

    // DON'T — every card mutation invalidates every reader of the catalog
    public sealed record CardCatalogState(IReadOnlyDictionary<CardId, CardData> ById) : State;

    // DO — one slice per CardId; per-card subscriptions are free
    public sealed record CardRuntimeState(int Damage, int BonusAtk) : State;
    store.RegisterSlice(cardId, new CardRuntimeState(0, 0));

### Hold the id, not the aggregate snapshot

    // DON'T — snapshot goes stale on next mutation
    private CardAggregate cachedCard;

    // DO — read live each time
    private CardId cardId;
    var live = store.Get<CardAggregate>(cardId);

### Specific subscriptions over `SubscribeAllReferences` in production

    // DON'T (in production code) — wakes for every card in the game
    scope.Events.SubscribeAllReferences<CardRuntimeState>((_, _, _) => rebuild.RequestRebuild());

    // DO
    scope.Events.Subscribe<CardRuntimeState>(myCardId, (_, _, _) => rebuild.RequestRebuild());

## Roadmap

Performance and ergonomics improvements tracked in `Plans/AggregateRebuildOptimization/AggregateRebuildOptimization-ExecPlan.md`. Milestone 1 (`IStateScope.TryGet<T>`) shipped.
