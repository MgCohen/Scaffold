# Stateful Catalog Registration

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository policy for ExecPlans is defined in `PLANS.md` at the repository root. This document must be maintained in accordance with that file.


## Purpose / Big Picture

After this work, a game can register a domain object (a card, a zone, a unit) with **one call** that does the full orchestration: allocates a stable identity in the catalog, brings the object's initial state slices into the store keyed by that identity, and returns a typed reference the caller can hold onto for later reads, mutations, subscriptions, and unregistration. Today this is a three-step manual choreography (`Catalog.Register(obj)` → loop and `Store.RegisterSlice(ref, state)` per slice → keep the ref somewhere) and the reverse is a fragile open-coded teardown. The friction we hit a few sessions ago — *"how do we orchestrate definition registration, slice registration, and the definition ↔ slice map?"* — is the gap this plan closes.

Someone can see it working when: a unit test constructs a small `Card` type that declares `Health` and `Owner` slices, calls `store.RegisterEntity(card)`, asserts that `store.Get<HealthState>(ref)` and `store.Get<OwnerState>(ref)` both resolve, calls `store.UnregisterEntity(ref)`, and asserts the catalog and both slices are gone with no leaked subscriptions.

This plan also serves as a deliberate **re-evaluation gate** for two design choices made in Plan A (PR #40):

  - Is `Ref<T>` carrying its weight, or can the orchestration use `Reference` directly?
  - Is `Reference` (the abstract record base, replacing the old `IReference` marker) still the right shape once a higher-level registration flow exists?

These questions are settled in the Decision Log before any code lands.


## Definitions

  - **Catalog** — Per-`Store` table mapping a `Reference` (currently `Ref<T>`) to an opaque object. Introduced in PR #40 (Plan A).

  - **Slice** — A `(Reference, StateType) → State` entry in the store. Created by `RegisterSlice` (canonical) or `RegisterAggregate` (computed).

  - **Entity** — In this plan, a plain C# object that (a) wants a stable catalog identity and (b) brings one or more initial slice values that should live under that identity. The term is local to this document; it does **not** imply a coupling to `com.scaffold.entities`. The orchestration applies to anything that opts in.

  - **Slice provider** — The proposed contract under which an object describes the slices it brings. The exact shape is one of the open decisions below.

  - **Stateful registration** — The composite operation: allocate identity in the catalog, register slices, return ref. Reverse: unregister slices, unregister catalog entry.


## Progress

  - [ ] Decisions D1–D5 below resolved with rationale recorded in the Decision Log.
  - [ ] Milestone 1 — Spike: prototype the chosen `ISliceProvider` shape against a throwaway `Card` test fixture, no production wiring. Confirm round-trip register / get / unregister.
  - [ ] Milestone 2 — Land the contract + extension methods in `com.scaffold.states` (or a thin layer above it — see D5). Unit + integration tests.
  - [ ] Milestone 3 — Migrate one in-repo consumer (likely `com.scaffold.entities.states` `EntityBridge`) onto the new flow as the validation case; delete the manual choreography it replaces.
  - [ ] Milestone 4 — Update Plan B (`Plans/new state unification/entities-state-unification.md`) so it references this contract instead of redefining its own.
  - [ ] Outcomes & Retrospective filled in.


## Surprises & Discoveries

Document unexpected behaviors, bugs, optimizations, or insights discovered during implementation. Provide concise evidence.

  - (none yet)


## Background — what we have today

  - **`Reference`** (`Assets/Packages/com.scaffold.states/Runtime/Abstractions/Reference.cs`) is an `abstract record` with a sealed `NullReference` singleton and three known descendants in-tree:

      - `NullReference` — `Reference.Null` sentinel.
      - `Ref<T>(Guid Id)` — Plan A's typed ref (PR #40).
      - `EntityStateReference(InstanceId EntityId)` — entity-package ref keyed by the entity instance id.

  - **`ICatalog`** (Plan A, PR #40) — `AllocateRef` / `RegisterAt` / `Register` / `Resolve` / `TryResolve` / `Unregister`. Backed by `Map<Reference, Type, object>`; the secondary `Type` key gives type-mismatch a clean miss instead of a silent cast.

  - **`Store.RegisterSlice(Reference?, State)`** and **`Store.UnregisterSlice<TState>(Reference?)`** — Slice lifecycle. No knowledge of catalog.

  - **`Store.Catalog`** — Read-only `ICatalog` property on every store. `StoreBuilder` does not touch it.

  - **No bridge today.** Catalog and slices are independent tables sharing only the `Reference` key type. Consumers wire them together by hand.


## Proposed shape (subject to D1–D4)

The strawman the rest of the plan validates or replaces:

    public interface ISliceProvider
    {
        IEnumerable<State> ProvideInitialSlices();
    }

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

The reader should treat the snippet above as a starting point, not a commitment. The Decision Log entries below pick the final shape.


## Decision Log

  - **D1 — Slice provider contract: data or behavior?** *(open)*

    Two candidate shapes:

      1. **Data-shaped:** `IEnumerable<State> ProvideInitialSlices()`. Caller iterates; orchestration logic lives in the extension method. Simplest; can't express aggregate-slice registration or per-slice configuration.

      2. **Behavior-shaped:** `void Provision(IStoreScope scope, Reference @ref)`. Entity drives its own registration; can call `RegisterAggregate`, `RegisterSlice`, anything the scope exposes. More expressive; couples the entity to `IStoreScope`.

    Default if not chosen: **data-shaped**. The expressive shape can be added later as a second interface (`IStoreProvisioner` or similar) without breaking data-shaped consumers.

    Decide: pick a default; record the call's date/author/rationale here.

  - **D2 — Symmetric unregister: re-call provider, or remember slices?** *(open)*

    Three candidates:

      1. Re-call `ProvideInitialSlices()` on unregister and use the returned states' `.GetType()` to unregister by type. **Risk:** if the entity returns different slice types between calls (e.g., conditional on internal state), unregister leaks slices.

      2. Cache the slice types on register inside the catalog entry itself. Means the catalog gains a per-entry side table, or `ICatalog` grows to know about state types. Couples the layers.

      3. Define `ProvideInitialSlices()` as a **stable enumeration** by contract — same slice **types** on every call, same count, same order — and rely on it. Document this as the contract.

    Default if not chosen: **(3) stable enumeration**. Cheap, doesn't grow the catalog, fails loudly if violated (slice unregister returns false → orchestration can throw or warn).

    Decide.

  - **D3 — Is `Ref<T>` carrying its weight?** *(open — re-eval gate)*

    Pro `Ref<T>`:

      - Type-safe `Catalog.Resolve<T>(Ref<T>)` — calling `Resolve<Zone>` on a card-ref is a compile-time error, not a runtime cast.
      - Ergonomic call sites: `var card = store.Catalog.Resolve(cardRef)` infers `T` from the ref.
      - Plays well with the proposed `RegisterEntity<T>(this Store, T) → Ref<T>` signature; the returned ref's `T` is the entity type, so `Resolve(ref)` round-trips without casts.

    Con `Ref<T>`:

      - Heap allocation per ref (acknowledged in PR #40).
      - The slice APIs all take `Reference`, so the `T` tag is **lost** the moment a `Ref<T>` is passed to `RegisterSlice` / `Get<TState>(ref)`. That means in the most common code paths, `T` is decorative.
      - Two refs into the same identity table — `Ref<Card>(g)` vs `Reference.Null` vs `EntityStateReference(id)` — already have to interoperate by virtue of the shared `Reference` base. The `T` gives no extra discrimination on the slice side.

    Candidate alternatives:

      1. **Keep `Ref<T>` as-is.** Type safety wins where it matters (catalog boundary). Cost is heap-per-ref at project scale, which Plan A already accepted.

      2. **Drop `Ref<T>`; `Catalog` keys on `Reference` directly with `Type` as secondary.** Loses compile-time `Resolve<T>` type-check on the ref, retains it via the explicit `T` argument to `Resolve<T>(Reference)`. Saves the per-ref allocation but degrades call-site ergonomics (`store.Catalog.Resolve<Card>(ref)` instead of `store.Catalog.Resolve(ref)`).

      3. **Make `Ref<T>` a `readonly struct`** — original Plan A draft. Loses `record` value-equality wiring; we'd hand-roll `IEquatable<Ref<T>>` and a hash combiner. Saves the heap but reintroduces the boxing problem when stored in `Map<Reference, ...>` (the secondary key would still box on store).

    Default if not chosen: **(1) keep `Ref<T>`**. The orchestration plan is the moment to remove `Ref<T>` if we're going to — once `RegisterEntity<T>` returns `Ref<T>` and the entity package depends on it, removal is a breaking churn. Either kill it now or commit.

    Decide.

  - **D4 — Is `Reference` (abstract record base) still the right shape?** *(open — re-eval gate)*

    What it does today:

      - Sealed-with-singleton `NullReference` provides `Reference.Null` for ambient/unkeyed slices.
      - Open-for-extension base for `Ref<T>` and `EntityStateReference`.
      - Record equality means `Map<Reference, Type, ...>` gets value-keyed lookups for free.

    What's friction:

      - The `NullReference` singleton lives **inside** `Reference.cs` as a nested `sealed record` — this is fine but it makes `Reference` not purely abstract; it's a base + a sentinel container. A reader skimming the file is mildly confused.
      - `Reference` has no members. It exists for two reasons: equality contract (record) and shared base type (for `where TRef : Reference` constraints). A non-empty marker would not earn its keep; an empty record does — barely.
      - Contrast with the old `IReference` marker interface: that one was deleted in the Phase 4 refactor (PR #35) precisely because empty markers leak no contract. The current `Reference` record at least guarantees value equality.

    Candidate alternatives:

      1. **Keep `Reference` as-is.** Empty abstract record. Status quo from PR #35.

      2. **Move `NullReference` out of `Reference.cs`** into its own file, leave `Reference` as a one-line abstract record. Smaller, easier-to-skim file. No behavior change.

      3. **Replace with a `sealed record Reference(string? Tag = null)`** holding nothing but a debug tag. Subclasses become factory methods (`Reference.For<Card>(guid)`). Smaller type tree, but loses the type-discrimination test in PR #40 (`RefEquality_SameGuidDifferentT_NotEqual`) — two refs differing only in `T` would compare equal.

    Default if not chosen: **(2) keep semantics, move `NullReference` to its own file**. Clarifies the file without disturbing any callers; leaves room for future descendants.

    Decide.

  - **D5 — Where does the orchestration live?** *(open)*

    Options:

      1. Extension methods on `Store` inside `com.scaffold.states`. Pulls `ISliceProvider` into the states package.

      2. Extension methods + interface in `com.scaffold.states.entities` (new asmdef) or `com.scaffold.entities.states` (existing). Keeps states package free of "entity" vocabulary; adds an asmdef boundary.

      3. Methods directly on `Store` (not extensions). Largest blast radius; commits the states API to the orchestration shape.

    Default if not chosen: **(1)**. The contract is generic ("things that bring slices") and doesn't mention entities; the states package can own it without leaking entity concepts. If a reviewer disagrees, fall back to (2).

    Decide.


## Interfaces and Dependencies

The orchestration touches:

  - **`com.scaffold.states`** (writes): adds `ISliceProvider` (or chosen contract from D1) and one or two extension classes. No changes to `Store`, `Catalog`, `Reference`, or `Ref<T>` beyond what D3/D4 mandate.

  - **`com.scaffold.states.Tests`** (writes): unit + integration tests covering the new flow.

  - **`com.scaffold.entities.states`** (Milestone 3 only — writes): migrate `EntityBridge` (or whichever class wires entities to slices today) onto the new flow. Validates the contract is shaped right.

  - **`Plans/new state unification/entities-state-unification.md`** (Milestone 4 — writes): update Plan B to defer to this plan instead of redefining the orchestration.

No external (Unity / package-manager) dependencies.


## Validation Gate / Exit Criteria

  - All decisions D1–D5 have a recorded outcome with rationale and date.
  - Unit tests cover: register success, unregister success, register-then-resolve round-trip, unregister-of-unregistered no-ops cleanly, register of an `ISliceProvider` that also implements `ICatalogged` keys by `ICatalogged.Key`, register-with-empty-slice-list works.
  - Integration test: an entity holds a `Ref<TOther>` to a sibling entity inside one of its slice values and survives a snapshot round-trip (subsumes the catalog-survives-snapshot test from Plan A).
  - At least one in-repo consumer (`com.scaffold.entities.states`) builds and tests green on the new flow.
  - `validate-changes.ps1` clean (or local equivalent if PowerShell unavailable).
  - Plan A's `Ref<T>` and `Reference` either confirmed as-is **with** a one-line note in their files referencing this plan's decision, or replaced per the chosen alternative.


## Outcomes & Retrospective

To be filled in after Milestone 3 lands.

  - What worked.
  - What surprised us (move from `Surprises & Discoveries`).
  - Whether D3 and D4 stayed at their defaults or flipped, and why.
  - Follow-up items for Plan B.
