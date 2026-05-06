# Stateful Catalog Registration

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository policy for ExecPlans is defined in `PLANS.md` at the repository root. This document must be maintained in accordance with that file.


## Purpose / Big Picture

After this work, a game can register a domain object (a card, a zone, a unit) with **one call** that does the full orchestration: allocates a stable identity in the catalog, brings the object's initial state slices into the store keyed by that identity, and returns a reference the caller can hold onto. Once held, that reference is the **single rooted handle** for everything downstream — `ref.Resolve<Card>(scope)` reaches the catalog object, `ref.GetSlice<HealthState>(scope)` reaches a slice, with the same `ref.Verb<T>(scope)` shape for both surfaces. Today this is a three-step manual choreography (`Catalog.Register(obj)` → loop and `Store.RegisterSlice(ref, state)` per slice → keep the ref somewhere) plus an asymmetric reach (catalog through `Catalog`, slices through `Store`). The friction we hit a few sessions ago — *"how do we orchestrate definition registration, slice registration, and the definition ↔ slice map?"* — is the gap this plan closes.

Someone can see it working when: a unit test constructs a small `Card` type that declares `HealthState` and `OwnerState` slices, calls `store.RegisterEntity(card)`, asserts that `ref.GetSlice<HealthState>(store)` and `ref.Resolve<Card>(store)` both succeed, calls `store.UnregisterEntity(ref)`, and asserts the catalog and both slices are gone with no leaked subscriptions.

This plan also resolves two design choices made in Plan A (PR #40), recorded in the Decision Log below:

  - **D3 — Drop `Ref<T>`, go non-generic `Ref(Guid Id) : Reference`.** The (Guid, Type) discrimination already lives in the catalog's secondary key, so `T` on the ref was duplication. Non-generic refs allow a single ref-rooted ergonomic surface (`ref.Resolve<T>(scope)`, `ref.GetSlice<T>(scope)`) and heterogeneous storage (`List<Ref>`).
  - **D4 — Keep `Reference` as the abstract record base; move `NullReference` to its own file.** Three live descendants justify the type; record equality is load-bearing for `Map<Reference, Type, …>`. Only cleanup is the nested-singleton-in-base-file smell.


## Definitions

  - **Catalog** — Per-`Store` table mapping a `Reference` to an opaque object, discriminated by a secondary `Type` key. Introduced in PR #40 (Plan A).

  - **Slice** — A `(Reference, StateType) → State` entry in the store. Created by `RegisterSlice` (canonical) or `RegisterAggregate` (computed).

  - **Ref** — Non-generic `sealed record Ref(Guid Id) : Reference`, the typed-by-context handle returned by catalog and entity registration. Replaces the generic `Ref<T>` from Plan A (see D3).

  - **Entity** — In this plan, a plain C# object that (a) wants a stable catalog identity and (b) brings one or more initial slice values that should live under that identity. The term is local to this document; it does **not** imply a coupling to `com.scaffold.entities`. The orchestration applies to anything that opts in.

  - **Slice provider** — The contract under which an object describes the slices it brings: `IEnumerable<State> ProvideInitialSlices()`. Required to be a *stable enumeration* — same slice **types**, same count, every call.

  - **Stateful registration** — The composite operation: allocate identity in the catalog, register slices, return ref. Reverse: unregister slices, unregister catalog entry.


## Progress

  - [x] Decisions D1–D5 below resolved with rationale recorded in the Decision Log.
  - [ ] Milestone 1 — `Ref<T>` → `Ref` migration in `com.scaffold.states` (D3). Update `Catalog`, `ICatalog`, tests. `NullReference` moved out of `Reference.cs` (D4).
  - [ ] Milestone 2 — Add `ISliceProvider`, `RegisterEntity` / `UnregisterEntity` extensions on `Store`, and ref-rooted resolve extensions on `Ref`. Unit + integration tests.
  - [ ] Milestone 3 — Migrate one in-repo consumer (likely `com.scaffold.entities.states` `EntityBridge`) onto the new flow. Delete the manual choreography it replaces.
  - [ ] Milestone 4 — Update Plan B (`Plans/new state unification/entities-state-unification.md`) so it references this contract instead of redefining its own.
  - [ ] Outcomes & Retrospective filled in.


## Surprises & Discoveries

Document unexpected behaviors, bugs, optimizations, or insights discovered during implementation. Provide concise evidence.

  - (none yet)


## Background — what we have today

  - **`Reference`** (`Assets/Packages/com.scaffold.states/Runtime/Abstractions/Reference.cs`) is an `abstract record` with a sealed `NullReference` singleton and three known descendants in-tree:

      - `NullReference` — `Reference.Null` sentinel.
      - `Ref<T>(Guid Id)` — Plan A's typed ref (PR #40). **This plan replaces it with non-generic `Ref(Guid Id)`.**
      - `EntityStateReference(InstanceId EntityId)` — entity-package ref keyed by the entity instance id.

  - **`ICatalog`** (Plan A, PR #40) — `AllocateRef` / `RegisterAt` / `Register` / `Resolve` / `TryResolve` / `Unregister`. Backed by `Map<Reference, Type, object>`; the secondary `Type` key gives type-mismatch a clean miss instead of a silent cast. **Signatures change from `Ref<T>` to `Ref` per D3** — the type argument moves to the call site (`Resolve<T>(Ref)` instead of `Resolve(Ref<T>)`).

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

The catalog primitive (post-D3):

    public interface ICatalog
    {
        Ref AllocateRef<T>();
        void RegisterAt<T>(Ref @ref, T obj);
        Ref Register<T>(T obj);
        T Resolve<T>(Ref @ref);
        bool TryResolve<T>(Ref @ref, [MaybeNullWhen(false)] out T obj);
        void Unregister<T>(Ref @ref);
    }

The orchestration:

    public static class StoreEntityExtensions
    {
        public static Ref RegisterEntity<T>(this Store store, T entity)
            where T : class, ISliceProvider
        {
            Ref @ref = store.Catalog.Register(entity);
            foreach (State state in entity.ProvideInitialSlices())
            {
                store.RegisterSlice(@ref, state);
            }
            return @ref;
        }

        public static bool UnregisterEntity<T>(this Store store, Ref @ref)
            where T : class, ISliceProvider
        {
            if (!store.Catalog.TryResolve(@ref, out T entity)) return false;
            foreach (State state in entity.ProvideInitialSlices())
            {
                store.UnregisterSlice(@ref, state.GetType());
            }
            store.Catalog.Unregister<T>(@ref);
            return true;
        }
    }

The ref-rooted sugar (extensions on `Ref`, in `com.scaffold.states`):

    public static class RefExtensions
    {
        public static T Resolve<T>(this Ref @ref, IStoreScope scope)
            => scope.Catalog.Resolve<T>(@ref);

        public static bool TryResolve<T>(this Ref @ref, IStoreScope scope, [MaybeNullWhen(false)] out T obj)
            => scope.Catalog.TryResolve(@ref, out obj);

        public static TState GetSlice<TState>(this Ref @ref, IStoreScope scope) where TState : BaseState
            => scope.Get<TState>(@ref);

        public static bool TryGetSlice<TState>(this Ref @ref, IStoreScope scope, out TState state) where TState : BaseState
            => scope.TryGet(@ref, out state);
    }

Refs stay **pure data** — no back-reference to a store. Extensions take an `IStoreScope` (or `ICatalog` for catalog-only calls). This keeps refs serializable, equatable across stores, and lifecycle-independent. The cost is one extra token at the call site (`scope`) in exchange for keeping the data model clean.


## Decision Log

  - **D1 — Slice provider contract: data or behavior?** *(resolved)*

    **Decision:** Data-shaped. `IEnumerable<State> ProvideInitialSlices()`.

    Rationale: Smallest contract that solves the friction. The expressive behavior-shaped variant (`void Provision(IStoreScope, Ref)`) can be added later as a second interface (`IStoreProvisioner` or similar) without breaking data-shaped consumers — neither contract knows about the other.

    Alternatives considered:

      - Behavior-shaped: `void Provision(IStoreScope scope, Ref @ref)`. More expressive (entity could call `RegisterAggregate`, custom registration logic). Couples the entity to `IStoreScope`. Rejected for v1; revisit only if Milestone 3 (`EntityBridge` migration) shows the data shape is too thin.

  - **D2 — Symmetric unregister: re-call provider, or remember slices?** *(resolved)*

    **Decision:** Re-call `ProvideInitialSlices()` and unregister by `state.GetType()`. Document the contract as *stable enumeration* — same slice **types** on every call, same count.

    Rationale: Cheap. Doesn't grow the catalog. Fails loudly if an entity violates the stability contract — `UnregisterSlice` returns `false` for missing types, and the orchestration can elevate that to a throw or warning.

    Alternatives considered:

      - Cache slice types per catalog entry. Couples catalog to slice types; rejected.
      - Trust the entity to also implement `IDisposable` or a custom teardown. Pushes orchestration back onto the caller; rejected.

  - **D3 — Is `Ref<T>` carrying its weight?** *(resolved — flip from Plan A)*

    **Decision:** Drop `Ref<T>`. Replace with non-generic `sealed record Ref(Guid Id) : Reference`. `T` moves to the call site on `Resolve<T>(Ref)` / `TryResolve<T>(Ref, out T)` / `Register<T>(T) → Ref`.

    Rationale:

      - The (Guid, Type) discrimination already lives in the catalog's secondary key. `T` on the ref was duplicating it.
      - `RegisterEntity<T>(T entity) → Ref` already has `T` from the argument. Carrying it into the return type is redundant.
      - Non-generic `Ref` enables heterogeneous storage (`List<Ref>`, `Dictionary<Ref, …>`) without generic wrangling.
      - Symmetric ref-rooted sugar: `ref.Resolve<T>(scope)` and `ref.GetSlice<TState>(scope)` both push `T` to the call site, giving one consistent surface. With `Ref<T>`, `Resolve` infers `T` but `GetSlice<TState>` still needs explicit T — asymmetric.
      - One closed generic instantiation gone per T (small AOT/IL2CPP win).
      - Cost: `catalog.Resolve(cardRef)` no longer infers `Card`. Caller writes `cardRef.Resolve<Card>(scope)`. Slightly more verbose but the field name (`Ref cardRef;`) carries intent for readers.

    The `RefEquality_SameGuidDifferentT_NotEqual` test from Plan A is deleted — Guid uniqueness from `Guid.NewGuid()` and explicit `ICatalogged.Key` declarations make accidental Guid sharing across types a non-real scenario.

  - **D4 — Is `Reference` (abstract record base) still the right shape?** *(resolved)*

    **Decision:** Keep `Reference` as-is semantically. Move `NullReference` out of `Reference.cs` into its own file.

    Rationale:

      - Three live descendants (`NullReference`, `Ref`, `EntityStateReference`) justify the type.
      - Record equality is load-bearing — every `Map<Reference, Type, …>` lookup uses it.
      - The "empty marker" concern doesn't apply here; an empty interface would be a marker, but an empty record carries equality semantics.
      - The only smell is the nested `sealed record NullReference` inside `Reference.cs` — splitting it into `Assets/Packages/com.scaffold.states/Runtime/Abstractions/NullReference.cs` is a 30-second cleanup with zero behavior change.

  - **D5 — Where does the orchestration live?** *(resolved)*

    **Decision:** `com.scaffold.states`. Both `ISliceProvider` and the `Store` / `Ref` extensions ship in the states package.

    Rationale: The contract is generic ("things that bring slices") and doesn't mention entities. Keeping it in `com.scaffold.states` means the entities package can consume it without a circular dependency, and any future non-entity consumer (a quest system, a buff system) can use the same surface.

    Alternative considered: a new `com.scaffold.states.entities` asmdef. Adds a boundary for no clear win since the contract is entity-agnostic.

  - **D6 — Does `Ref` carry a back-reference to its store?** *(resolved)*

    **Decision:** No. Refs stay pure data (just a `Guid`). Resolution and slice access go through extension methods that take an `IStoreScope`.

    Rationale:

      - Refs are serializable, equatable across stores, and lifecycle-independent today. That's a strong property worth keeping.
      - A back-reference would couple ref lifetime to store lifetime — disposing a store would leave dangling refs; snapshot save/load would need re-binding hooks.
      - The ergonomic loss is one token at the call site (`ref.Resolve<T>(scope)` vs. `ref.Resolve<T>()`). Small price.
      - Autocomplete still surfaces all the verbs on `ref.` — discoverability is unaffected.


## Interfaces and Dependencies

The orchestration touches:

  - **`com.scaffold.states`** (writes):

      - **Replace** `Ref<T>` with non-generic `Ref` (D3). Update `ICatalog`, `Catalog`, all internal callers.
      - **Move** `NullReference` to its own file (D4).
      - **Add** `ISliceProvider`.
      - **Add** `StoreEntityExtensions` (`RegisterEntity` / `UnregisterEntity`).
      - **Add** `RefExtensions` (`Resolve<T>` / `TryResolve<T>` / `GetSlice<TState>` / `TryGetSlice<TState>`).

  - **`com.scaffold.states.Tests`** (writes):

      - Update Plan A's `CatalogTests` and `CatalogIntegrationTests` for the `Ref<T>` → `Ref` migration. Delete `RefEquality_SameGuidDifferentT_NotEqual` per D3.
      - Add unit file for `ISliceProvider` + `RegisterEntity` / `UnregisterEntity`: register success, unregister success, register-then-resolve round-trip, unregister-of-unregistered no-ops cleanly, register of an `ISliceProvider` that also implements `ICatalogged` keys by `ICatalogged.Key`, register-with-empty-slice-list works.
      - Add integration test: an entity holds a `Ref` to a sibling entity inside one of its slice values and survives a snapshot round-trip (subsumes Plan A's catalog-survives-snapshot test).

  - **`com.scaffold.entities.states`** (Milestone 3 — writes):

      - Migrate `EntityBridge` (or whichever class wires entities to slices today) onto the new flow.
      - `EntityStateReference` audit: with non-generic `Ref` available, evaluate whether `EntityStateReference(InstanceId)` is still earning its keep or whether entity registration should just yield a `Ref(InstanceId.AsGuid())`. Record outcome in the entities migration's own commit message; not a blocker for this plan.

  - **`Plans/new state unification/entities-state-unification.md`** (Milestone 4 — writes):

      - Update Plan B to defer to this plan instead of redefining the orchestration.
      - Reflect the `Ref<T>` → `Ref` decision in any code samples there.

No external (Unity / package-manager) dependencies.


## Validation Gate / Exit Criteria

  - All decisions D1–D6 have a recorded outcome with rationale. *(D1–D6 done above.)*
  - `Ref<T>` is gone from the codebase (compile-fail check).
  - `NullReference` lives in its own file.
  - Unit tests cover: register success, unregister success, register-then-resolve round-trip, unregister-of-unregistered no-ops cleanly, register of an `ISliceProvider` that also implements `ICatalogged` keys by `ICatalogged.Key`, register-with-empty-slice-list works.
  - Integration test: an entity holds a `Ref` to a sibling entity inside one of its slice values and survives a snapshot round-trip.
  - Ref-rooted sugar tested at least once each (`ref.Resolve<T>(scope)` and `ref.GetSlice<TState>(scope)`).
  - At least one in-repo consumer (`com.scaffold.entities.states`) builds and tests green on the new flow.
  - `validate-changes.ps1` clean (or local equivalent if PowerShell unavailable).


## Outcomes & Retrospective

To be filled in after Milestone 3 lands.

  - What worked.
  - What surprised us (move from `Surprises & Discoveries`).
  - Whether the ref-rooted sugar was actually used in the migrated `EntityBridge` site, or whether callers preferred `scope.Get<…>(ref)` style — informs whether the `RefExtensions` surface stays or shrinks.
  - Whether D1's data-shaped decision held, or whether the behavior-shaped `IStoreProvisioner` second interface ended up needed.
  - Follow-up items for Plan B.
