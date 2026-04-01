# Prototype: dependency ordering (groups only)

This document plans a **small, throwaway or keepable prototype** that validates **ordering only**: given a built `IObjectResolver` and a marker interface, produce **ordered groups** (topological levels). It does **not** run `InitializeAsync` or any other initialization logic.

**Related:** [Startup: initialization ordering](Startup-Initialization-Ordering.md) — full init pipeline, `IAsyncInitializable`, waves, and production concerns. This prototype reuses the **same graph rules** (membership, `TryGetRegistration`, ctor/inject reflection) but stops at **group computation**.

---

## Goal

After `Build()`, you can call a single API that returns something like **`IReadOnlyList<IReadOnlyList<Type>>`** where:

- Inner lists are **parallel-safe groups** (same dependency level).
- Outer list is **dependency order** (earlier groups must complete before later groups **if** you later add init—here we only **compute** groups).
- Types in the groups come from **all registrations** of a **minimal marker interface**, grouped by concrete type (duplicates allowed in resolution, same as the main plan).

**Non-goals for this prototype**

- No `InitializeAsync`, no `Task.WhenAll` execution.
- No migration from `IAsyncLayerInitializable`.
- No new production bootstrap wiring unless you choose to promote the prototype.

---

## Minimal interface

The smallest useful contract is a **marker only** (no members). Ordering is derived from **concrete type** + **DI graph**, not from methods on the interface.

```csharp
namespace Scaffold.Scope.Contracts // or a tiny prototype namespace
{
    /// <summary>Marker: type participates in startup ordering. No behavior on the interface.</summary>
    public interface IStartupOrderParticipant
    {
    }
}
```

**Registration:** Each service that should appear in ordering includes `.As<IStartupOrderParticipant>()` (alongside its normal service interfaces). Same pattern as `.As<IAsyncInitializable>()` in the main doc.

---

## Prototype API

Keep the surface **tiny**: one type that takes a resolver (or receives the pre-resolved list + resolver for `TryGetRegistration` only).

**Suggested shape**

```csharp
public interface IStartupOrderGroupProvider
{
    /// <summary>
    /// Eager-resolve all IStartupOrderParticipant, build graph, return topological levels as groups of concrete types.
    /// </summary>
    IReadOnlyList<IReadOnlyList<Type>> GetOrderedGroups(IObjectResolver resolver);
}
```

**Optional overload for tests** (no container): pass **`IReadOnlyList<IStartupOrderParticipant>`** + **`IObjectResolver`** so tests do not need to mock collection resolve.

```csharp
IReadOnlyList<IReadOnlyList<Type>> GetOrderedGroups(
    IReadOnlyList<IStartupOrderParticipant> participants,
    IObjectResolver resolver);
```

**Output interpretation**

- Each **`Type`** is a **concrete** implementation type (`typeof(T)`).
- If multiple instances share a type, that type appears **once** per level; **execution** (later) would run all instances in that bucket—this prototype only returns **type groups**.

---

## Pipeline (what the prototype implements)

1. **Resolve list:** `resolver.Resolve<IReadOnlyList<IStartupOrderParticipant>>()` (or `IEnumerable<…>` — match VContainer; same note as the main plan).
2. **Membership:** Distinct **`GetType()`** keys; optional **`ILookup<Type, IStartupOrderParticipant>`** if you need to attach instances later (not required for “groups of types” only).
3. **Edges:** For each participating concrete type `T`, walk **ctor + `[Inject]` methods/fields/properties** (same rules as VContainer / [Startup-Initialization-Ordering.md](Startup-Initialization-Ordering.md)). For each dependency, **`resolver.TryGetRegistration(…)`** → `ImplementationType`. Add edge `depImpl → T` only if **both** are in membership.
4. **Levels:** Run **Kahn / topological levels** on the participant-only subgraph. **Cycle** → throw with a clear message.
5. **Return** `IReadOnlyList<IReadOnlyList<Type>>`.

No `InitializeAsync`, no per-wave side effects.

---

## Implementation notes

| Topic | Prototype choice |
|-------|------------------|
| **Graph building** | Inline or a private `InitializableDependencyWalker`-shaped helper; copy only what you need from the main plan. |
| **Duplicates** | Membership is **set of types**; ordering list does not duplicate a type across levels. |
| **Policies** | Follow [Resolved design decisions](Startup-Initialization-Ordering.md#resolved-design-decisions) in the main doc (per container, VContainer `TryGetRegistration`, optional ctor params in graph, etc.). |
| **Where to put code** | Prefer **`Scaffold.Scope`** (or a `*.Tests` prototype project) so VContainer is available; keep asmdef references minimal. |

---

## Snippet: skeleton implementation

```csharp
public sealed class StartupOrderGroupProvider : IStartupOrderGroupProvider
{
    public IReadOnlyList<IReadOnlyList<Type>> GetOrderedGroups(IObjectResolver resolver)
    {
        IReadOnlyList<IStartupOrderParticipant> participants =
            resolver.Resolve<IReadOnlyList<IStartupOrderParticipant>>();
        return GetOrderedGroups(participants, resolver);
    }

    public IReadOnlyList<IReadOnlyList<Type>> GetOrderedGroups(
        IReadOnlyList<IStartupOrderParticipant> participants,
        IObjectResolver resolver)
    {
        HashSet<Type> membership = participants.Select(p => p.GetType()).ToHashSet();
        var edges = new List<(Type From, Type To)>();
        foreach (Type type in membership)
        {
            // Add edges via ctor/inject reflection + TryGetRegistration (see main plan)
        }

        return ComputeLevelsAsReadOnly(membership, edges);
    }
}
```

Use an instance of **`StartupOrderTopologicalLevels`** and call **`Compute`** — same levelization as in [Startup-Initialization-Ordering.md](Startup-Initialization-Ordering.md).

---

## How to validate the prototype

1. **Unit tests** with a **real `ContainerBuilder`** (VContainer): register 3–4 dummy types `A → B → C` and one independent `D`; mark all as `IStartupOrderParticipant`; assert group count and that `D` is in an earlier or parallel group vs `C` as expected.
2. **Cycle test:** `A` depends on `B`, `B` on `A` (both participants) → expect **throw**.
3. **Non-participant dependency:** `A` depends on `Logger` (not a participant) → **no edge** from logger; ordering still valid.

---

## Deliverables checklist

- [x] `IStartupOrderParticipant` (marker) in `Scaffold.Scope` contracts folder.
- [x] `IStartupOrderGroupProvider` + `StartupOrderGroupProvider`.
- [x] Graph + level computation (`StartupOrderInjectSiteAnalyzer`, `StartupOrderTopologicalLevels`).
- [x] One EditMode test: `StartupOrderGroupProviderTests.GetOrderedGroups_ResolvesDependencyChainIntoThreeLevels` in `Scaffold.Scope.Tests`.
- [ ] Optional: register `StartupOrderGroupProvider` in production bootstrap (not required for the prototype).

---

## Promotion path

If the prototype is kept: rename or alias `IStartupOrderParticipant` to match production **`IAsyncInitializable`**, or have types implement **both** during migration. The **group provider** can remain separate from **`AsyncInitializationRunner`** (runner = groups + `InitializeAsync`).
