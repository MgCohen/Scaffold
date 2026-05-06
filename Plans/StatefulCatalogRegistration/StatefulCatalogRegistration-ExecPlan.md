# Stateful Catalog Registration

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository policy for ExecPlans is defined in `PLANS.md` at the repository root. This document must be maintained in accordance with that file.


## Purpose / Big Picture

After this work, a game can register a domain object (a card, a zone, a unit) with **one call** that does the full orchestration: allocates a stable identity in the catalog, brings the object's initial state slices into the store keyed by that identity, and returns a typed reference the caller can hold onto. Once held, that reference is the **rooted handle** for everything downstream — `cardRef.Resolve(scope)` reaches the catalog object (with `T` inferred from the ref), `cardRef.GetSlice<HealthState>(scope)` reaches a slice. Today this is a three-step manual choreography (`Catalog.Register(obj)` → loop and `Store.RegisterSlice(ref, state)` per slice → keep the ref somewhere) plus an asymmetric reach (catalog through `Catalog`, slices through `Store`). The friction we hit a few sessions ago — *"how do we orchestrate definition registration, slice registration, and the definition ↔ slice map?"* — is the gap this plan closes.

Someone can see it working when: a unit test constructs a small `Card` type that declares `HealthState` and `OwnerState` slices, calls `store.RegisterEntity(card)`, asserts that `cardRef.GetSlice<HealthState>(store)` and `cardRef.Resolve(store)` both succeed, calls `store.UnregisterEntity(cardRef)`, and asserts the catalog and both slices are gone with no leaked subscriptions.

This plan also resolves two design choices made in Plan A (PR #40), recorded in the Decision Log below:

  - **D3 — Keep `Ref<T>`.** Type-safe slice fields and collections (`Ref<Card> Owner`, `IReadOnlyList<Ref<Card>> Cards`) are compiler-enforced and worth the closed-generic-per-T cost. `Reference` is the heterogeneous base when mixed-type storage is genuinely needed.
  - **D4 — Keep `Reference` as the abstract record base; move `NullReference` to its own file.** Three live descendants justify the type; record equality is load-bearing for `Map<Reference, Type, …>`. Only cleanup is the nested-singleton-in-base-file smell.


## Definitions

  - **Catalog** — Per-`Store` table mapping a `Reference` to an opaque object, discriminated by a secondary `Type` key. Introduced in PR #40 (Plan A).

  - **Slice** — A `(Reference, StateType) → State` entry in the store. Created by `RegisterSlice` (canonical) or `RegisterAggregate` (computed).

  - **`Ref<T>`** — Typed handle, `sealed record Ref<T>(Guid Id) : Reference`. Returned by catalog and entity registration. The `T` is the catalog object type, not the state type. Carried into slice-value fields for compiler-enforced typing.

  - **`Reference`** — Base abstract record. Used as the heterogeneous storage type when a collection legitimately holds refs to different catalog types, and as the receiver type for slice-access extensions (`Reference.GetSlice<TState>`).

  - **Entity** — In this plan, a plain C# object that (a) wants a stable catalog identity and (b) brings one or more initial slice values that should live under that identity. The term is local to this document; it does **not** imply a coupling to `com.scaffold.entities`. The orchestration applies to anything that opts in.

  - **Slice provider** — The contract under which an object describes the slices it brings: `IEnumerable<State> ProvideInitialSlices()`. Required to be a *stable enumeration* — same slice **types**, same count, every call.

  - **Stateful registration** — The composite operation: allocate identity in the catalog, register slices, return ref. Reverse: unregister slices, unregister catalog entry.


## Progress

  - [x] Decisions D1–D6 below resolved with rationale recorded in the Decision Log.
  - [x] Milestone 1 — `NullReference` moved into `Assets/Packages/com.scaffold.states/Runtime/Abstractions/NullReference.cs` (D4).
  - [x] Milestone 2 — Added `ISliceProvider`, `StoreEntityExtensions` (`RegisterEntity` / `UnregisterEntity`), `RefExtensions` (`Resolve` / `TryResolve` on `Ref<T>`), `ReferenceExtensions` (`GetSlice` / `TryGetSlice` on `Reference`); `IStoreScope` gained `ICatalog Catalog { get; }` so the ref-rooted sugar can take a single scope arg. Unit + snapshot-round-trip integration test in `StatefulRegistrationTests.cs`. **Pending Unity Test Runner pass** (no local Unity available in this environment).
  - [ ] Milestone 3 — Migrate one in-repo consumer (likely `com.scaffold.entities.states` `EntityBridge`) onto the new flow. Delete the manual choreography it replaces.
  - [ ] Milestone 4 — Update Plan B (`Plans/new state unification/entities-state-unification.md`) so it references this contract instead of redefining its own.
  - [ ] Outcomes & Retrospective filled in.


## Surprises & Discoveries

Document unexpected behaviors, bugs, optimizations, or insights discovered during implementation. Provide concise evidence.

  - (none yet)


## Background — what we have today

  - **`Reference`** (`Assets/Packages/com.scaffold.states/Runtime/Abstractions/Reference.cs`) is an `abstract record` with a sealed `NullReference` singleton and three known descendants in-tree:

      - `NullReference` — `Reference.Null` sentinel.
      - `Ref<T>(Guid Id)` — Plan A's typed ref (PR #40). **Kept as-is per D3.**
      - `EntityStateReference(InstanceId EntityId)` — entity-package ref keyed by the entity instance id.

  - **`ICatalog`** (Plan A, PR #40) — `AllocateRef<T>` / `RegisterAt<T>` / `Register<T>` / `Resolve<T>` / `TryResolve<T>` / `Unregister<T>`, all keyed on `Ref<T>`. Backed by `Map<Reference, Type, object>`; the secondary `Type` key gives type-mismatch a clean miss instead of a silent cast. Unchanged.

  - **`Store.RegisterSlice(Reference?, State)`** and **`Store.UnregisterSlice<TState>(Reference?)`** — Slice lifecycle. No knowledge of catalog. Unchanged.

  - **`Store.Catalog`** — Read-only `ICatalog` property on every store. `StoreBuilder` does not touch it. Unchanged.

  - **No bridge today.** Catalog and slices are independent tables sharing only the `Reference` key type. Consumers wire them together by hand.


## Target shape

The contract:

    public interface ISliceProvider
    {
        // Stable enumeration: same slice TYPES on every call, same count.
        // Used at register-time to seed slices and at unregister-time to
        // collect the types to unregister. Values are read once at register.
        IEnumerable<State> ProvideInitialSlices();
    }

The orchestration:

    public static class StoreEntityExtensions
    {
        public static Ref<T> RegisterEntity<T>(this Store store, T entity)
            where T : class, ISliceProvider
        {
            Ref<T> @ref = store.Catalog.Register(entity);
            foreach (State state in entity.ProvideInitialSlices())
            {
                store.RegisterSlice(@ref, state);
            }
            return @ref;
        }

        public static bool UnregisterEntity<T>(this Store store, Ref<T> @ref)
            where T : class, ISliceProvider
        {
            if (!store.Catalog.TryResolve(@ref, out T entity)) return false;
            foreach (State state in entity.ProvideInitialSlices())
            {
                store.UnregisterSlice(@ref, state.GetType());
            }
            store.Catalog.Unregister(@ref);
            return true;
        }
    }

The ref-rooted sugar — split deliberately between `Ref<T>` and `Reference`:

    // Catalog access — receiver is Ref<T>, T inferred from the ref itself.
    public static class RefExtensions
    {
        public static T Resolve<T>(this Ref<T> @ref, IStoreScope scope)
            => scope.Catalog.Resolve(@ref);

        public static bool TryResolve<T>(this Ref<T> @ref, IStoreScope scope, [MaybeNullWhen(false)] out T obj)
            => scope.Catalog.TryResolve(@ref, out obj);
    }

    // Slice access — receiver is Reference (the base). TState is the state
    // type and is unrelated to the ref's catalog T, so this lives on the base
    // and works for Ref<T>, EntityStateReference, Reference.Null, etc.
    public static class ReferenceExtensions
    {
        public static TState GetSlice<TState>(this Reference @ref, IStoreScope scope) where TState : BaseState
            => scope.Get<TState>(@ref);

        public static bool TryGetSlice<TState>(this Reference @ref, IStoreScope scope, out TState state) where TState : BaseState
            => scope.TryGet(@ref, out state);
    }

Why split: putting `GetSlice<TState>` on `Ref<T>` would force callers to write `cardRef.GetSlice<Card, HealthState>(scope)` because C# can't half-infer (T from receiver, TState explicit). Putting it on `Reference` keeps the call as `cardRef.GetSlice<HealthState>(scope)` and incidentally makes it work for any `Reference` subtype.

Refs stay **pure data** — no back-reference to a store. Extensions take an `IStoreScope`. This keeps refs serializable, equatable across stores, and lifecycle-independent.


## Decision Log

  - **D1 — Slice provider contract: data or behavior?** *(resolved)*

    **Decision:** Data-shaped. `IEnumerable<State> ProvideInitialSlices()`.

    Rationale: Smallest contract that solves the friction. The expressive behavior-shaped variant (`void Provision(IStoreScope, Reference)`) can be added later as a second interface (`IStoreProvisioner` or similar) without breaking data-shaped consumers — neither contract knows about the other.

    Alternatives considered:

      - Behavior-shaped: `void Provision(IStoreScope scope, Reference @ref)`. More expressive (entity could call `RegisterAggregate`, custom registration logic). Couples the entity to `IStoreScope`. Rejected for v1; revisit only if Milestone 3 (`EntityBridge` migration) shows the data shape is too thin.

  - **D2 — Symmetric unregister: re-call provider, or remember slices?** *(resolved)*

    **Decision:** Re-call `ProvideInitialSlices()` and unregister by `state.GetType()`. Document the contract as *stable enumeration* — same slice **types** on every call, same count.

    Rationale: Cheap. Doesn't grow the catalog. Fails loudly if an entity violates the stability contract — `UnregisterSlice` returns `false` for missing types, and the orchestration can elevate that to a throw or warning.

    Alternatives considered:

      - Cache slice types per catalog entry. Couples catalog to slice types; rejected.
      - Trust the entity to also implement `IDisposable` or a custom teardown. Pushes orchestration back onto the caller; rejected.

  - **D3 — Is `Ref<T>` carrying its weight?** *(resolved — keep)*

    **Decision:** Keep `Ref<T>` as Plan A defined it. Use `Reference` as the heterogeneous base when collections legitimately hold mixed-type refs.

    Rationale:

      - **Slice values that hold refs are the central use case for this orchestration.** Entities point at entities — `record CardOwnerState(Ref<Card> Owner) : State` and `record ZoneState(IReadOnlyList<Ref<Card>> Cards) : State` are the bread and butter. With non-generic `Ref`, those become `Ref Owner` and `IReadOnlyList<Ref> /* of cards */` — the type discipline collapses to a comment. That's a real safety regression, not a paper one.
      - The (Guid, Type) discrimination in the catalog protects against runtime mismatch; it does **not** protect against accidentally putting a `Ref<Zone>` into a `Ref<Card>`-shaped field at compile time. Only the typed ref does.
      - Heterogeneous storage of mixed-type refs is rare. When it does come up, `List<Reference>` or `List<Reference>` already covers it via the base type.
      - Closed-generic-per-T AOT cost is real but small; the field-typing safety win pays for it many times over.

    What we explicitly *do* keep from the earlier non-generic exploration:

      - Ref-rooted sugar via extensions, not via refs holding back-references (D6).
      - The split surface: `Ref<T>` for catalog access (T inferred), `Reference` for slice access (state type unrelated to ref's T). Documented in **Target shape** above.

    Plan A's `RefEquality_SameGuidDifferentT_NotEqual` test stays.

  - **D4 — Is `Reference` (abstract record base) still the right shape?** *(resolved)*

    **Decision:** Keep `Reference` as-is semantically. Move `NullReference` out of `Reference.cs` into its own file.

    Rationale:

      - Three live descendants (`NullReference`, `Ref<T>`, `EntityStateReference`) justify the type.
      - Record equality is load-bearing — every `Map<Reference, Type, …>` lookup uses it.
      - The "empty marker" concern doesn't apply here; an empty interface would be a marker, but an empty record carries equality semantics — and now also serves as the receiver type for `GetSlice<TState>` extensions.
      - The only smell is the nested `sealed record NullReference` inside `Reference.cs` — splitting it into `Assets/Packages/com.scaffold.states/Runtime/Abstractions/NullReference.cs` is a 30-second cleanup with zero behavior change.

  - **D5 — Where does the orchestration live?** *(resolved)*

    **Decision:** `com.scaffold.states`. `ISliceProvider`, `StoreEntityExtensions`, `RefExtensions`, and `ReferenceExtensions` all ship in the states package.

    Rationale: The contract is generic ("things that bring slices") and doesn't mention entities. Keeping it in `com.scaffold.states` means the entities package can consume it without a circular dependency, and any future non-entity consumer (a quest system, a buff system) can use the same surface.

    Alternative considered: a new `com.scaffold.states.entities` asmdef. Adds a boundary for no clear win since the contract is entity-agnostic.

  - **D6 — Does `Ref<T>` carry a back-reference to its store?** *(resolved)*

    **Decision:** No. Refs stay pure data (just a `Guid`). Resolution and slice access go through extension methods that take an `IStoreScope`.

    Rationale:

      - Refs are serializable, equatable across stores, and lifecycle-independent today. That's a strong property worth keeping.
      - A back-reference would couple ref lifetime to store lifetime — disposing a store would leave dangling refs; snapshot save/load would need re-binding hooks.
      - The ergonomic loss is one token at the call site (`ref.Resolve(scope)` vs. `ref.Resolve()`). Small price.
      - Autocomplete still surfaces all the verbs on `ref.` — discoverability is unaffected.


## Interfaces and Dependencies

The orchestration touches:

  - **`com.scaffold.states`** (writes):

      - **Move** `NullReference` to its own file (D4).
      - **Add** `ISliceProvider`.
      - **Add** `StoreEntityExtensions` (`RegisterEntity<T>` / `UnregisterEntity<T>`).
      - **Add** `RefExtensions` (`Resolve<T>` / `TryResolve<T>` on `Ref<T>`).
      - **Add** `ReferenceExtensions` (`GetSlice<TState>` / `TryGetSlice<TState>` on `Reference`).

  - **`com.scaffold.states.Tests`** (writes):

      - Add unit file for `ISliceProvider` + `RegisterEntity` / `UnregisterEntity`: register success, unregister success, register-then-resolve round-trip, unregister-of-unregistered no-ops cleanly, register of an `ISliceProvider` that also implements `ICatalogged` keys by `ICatalogged.Key`, register-with-empty-slice-list works.
      - Add tests exercising the ref-rooted sugar: `cardRef.Resolve(store)` infers the type, `cardRef.GetSlice<HealthState>(store)` works, `Reference.Null.GetSlice<GameState>(store)` works.
      - Add integration test: an entity holds a `Ref<Card>` field on one of its slice values and survives a snapshot round-trip (extends Plan A's catalog-survives-snapshot tests with the orchestration-driven registration path).

  - **`com.scaffold.entities.states`** (Milestone 3 — writes):

      - Migrate `EntityBridge` (or whichever class wires entities to slices today) onto the new flow.
      - `EntityStateReference` audit: with the orchestration in place, evaluate whether `EntityStateReference(InstanceId)` is still earning its keep or whether entity registration should yield a `Ref<TEntity>`. Record outcome in the entities migration's own commit message; not a blocker for this plan.

  - **`Plans/new state unification/entities-state-unification.md`** (Milestone 4 — writes):

      - Update Plan B to defer to this plan instead of redefining the orchestration.


No external (Unity / package-manager) dependencies.


## Validation Gate / Exit Criteria

  - All decisions D1–D6 have a recorded outcome with rationale. *(D1–D6 done above.)*
  - `NullReference` lives in its own file.
  - Unit tests cover: register success, unregister success, register-then-resolve round-trip, unregister-of-unregistered no-ops cleanly, register of an `ISliceProvider` that also implements `ICatalogged` keys by `ICatalogged.Key`, register-with-empty-slice-list works.
  - Integration test: an entity holds a `Ref<TOther>` field on one of its slice values and survives a snapshot round-trip.
  - Ref-rooted sugar tested at least once each: `Ref<T>.Resolve(scope)` infers `T`; `Reference.GetSlice<TState>(scope)` works on both `Ref<T>` and `Reference.Null`.
  - At least one in-repo consumer (`com.scaffold.entities.states`) builds and tests green on the new flow.
  - `validate-changes.ps1` clean (or local equivalent if PowerShell unavailable).


## Outcomes & Retrospective

To be filled in after Milestone 3 lands.

  - What worked.
  - What surprised us (move from `Surprises & Discoveries`).
  - Whether the split-receiver sugar (`Ref<T>` for `Resolve`, `Reference` for `GetSlice`) felt natural at call sites or whether one or both should be dropped.
  - Whether D1's data-shaped decision held, or whether the behavior-shaped `IStoreProvisioner` second interface ended up needed.
  - Follow-up items for Plan B.
