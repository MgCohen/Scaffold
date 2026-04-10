# Entities Audit Findings

## Scope

This document captures the audit of `com.scaffold.entities` from high-level structure down to low-level allocation behavior. The review focused on:

- definition and instance layering
- flyweight usage and identity
- structural consistency between documentation, API, and runtime behavior
- optimization opportunities and their trade-offs

## High-Level Assessment

The package structure is fundamentally sound. The system clearly separates:

- shared authored defaults in `EntityDefinition`
- runtime-only base values in `instanceBaseBag`
- modifier-derived overlay values in `instanceEffectiveBag`

That gives the package a strong ownership model. Definitions remain shared and stable, while instances hold only per-entity state.

The bag chain is coherent:

- reads go through the effective bag first
- unresolved values fall back to runtime base values
- unresolved runtime values fall back to definition values
- recalculation reads from the base chain rather than from the effective layer

This avoids turning the effective cache into a competing source of truth.

## Pattern Audit

### Definition / instance split

The package is using a good definition-plus-instance model. `EntityDefinition` behaves like shared authored data, while `EntityInstance<TDefinition>` behaves like the mutable runtime container.

This is a good fit for Unity because authored data lives naturally in `ScriptableObject` assets, while runtime state remains instance-scoped.

### Flyweight usage

The current design uses the flyweight pattern correctly at a broad architectural level, but not as a strict runtime flyweight implementation.

What is working well:

- `AttributeSO` acts as a shared authoring asset
- `EntityDefinition` shares default values across instances
- entities do not duplicate effective values unless modifiers are active

What is weaker than a strict flyweight:

- runtime keys are recreated rather than interned
- key identity is derived from `AttributeSO.name`
- effective values are recomputed as new `AttributeValue` objects when modifiers apply

So the package is best described as a shared-definition system with flyweight characteristics, rather than a pure flyweight implementation.

### Identity risk

The largest pattern-level weakness is that runtime `Attribute` keys are derived from the attribute asset name and value type, not from a stable asset identity.

This makes the pattern more fragile than it first appears. A rename changes the effective runtime identity, which may become a problem for:

- persistence
- save/load systems
- cross-system references
- network synchronization
- long-lived cached lookups

The current approach is readable and convenient, but it trades long-term key stability for simplicity.

## Structural Audit

### Ownership boundaries

The boundaries between authored state and runtime state are good.

- `EntityDefinition` owns authored base entries
- `instanceBaseBag` owns runtime-added base attributes
- `instanceEffectiveBag` owns derived modifier output only
- `AttributeNotifier` owns value-change notifications
- `AttributeBag` structural events are limited to base-slot add/remove behavior

That split is easy to reason about and reduces accidental coupling between structural changes and value recalculation.

### Effective layer design

The effective bag is intentionally a cache layer, not a durable state layer. That is a good design decision because it keeps recalculation isolated from base value ownership.

The important invariant is that recalculation reads from the base chain, not from previously computed effective values. That keeps modifiers from compounding incorrectly.

### Hidden invariant in `AttributeBag`

`AttributeBag` has two representations:

- serialized `entries`
- runtime `localCache`

`RebuildCache()` rebuilds from serialized entries only. Runtime additions through `Add()` only affect `localCache`.

That is acceptable with the current usage pattern because `RebuildCache()` is used during setup, not during normal runtime mutation. But it is still a hidden invariant: future changes that call `RebuildCache()` after runtime mutations could unintentionally discard runtime-only values.

This is not a bug in the current flow, but it is a structural sharp edge.

### Documentation drift

There is some drift between the README and the code:

- the README still mentions `EntityBehaviour` naming, while the code uses `EntityComponent`
- the README mentions `InstanceId.New()`, while the implementation constructs `InstanceId` directly

This does not break runtime behavior, but it weakens the package's clarity and can mislead future work.

## Optimization And Allocation Audit

### 1. Highest-value low-risk optimization: cache converted attribute keys

The most obvious steady-state allocation comes from implicit conversion from `AttributeSO` to `Attribute`.

This matters most when gameplay code repeatedly calls APIs like:

- `TryGetAttribute`
- `GetValue<T>`
- `Subscribe`

using `AttributeSO` references in update loops or frequent polling.

The sample behavior demonstrates that pattern. In hot paths, repeatedly converting the same `AttributeSO` into a new `Attribute` object creates avoidable GC pressure.

Low-risk improvement:

- cache the converted `Attribute` once at the call site or initialization boundary

Trade-off:

- very small increase in boilerplate
- slightly less ergonomic than relying on implicit conversion everywhere

This is the best optimization candidate because it improves hot-path behavior without changing the package design.

### 2. Modifier recomputation allocates a new effective value object

When modifiers exist, recomputation calls `Combine()` and creates a new concrete `AttributeValue`.

This is clean and readable. It also keeps effective values isolated from base values. The cost is one allocation per recomputation for modified keys.

This is usually acceptable because recomputation happens on modifier changes, not on every read.

When it becomes worth caring about:

- modifier churn is high
- many entities add and remove modifiers frequently
- recalculation happens in bursts

Trade-off of optimizing this:

- reusing mutable effective objects would reduce allocations
- but it would increase mutation surface, make the system harder to reason about, and raise aliasing risks

This is a valid optimization target only after profiling demonstrates that modifier churn is a real cost center.

### 3. `StringAttributeValue.Combine()` creates extra string churn

String combination uses repeated concatenation in a loop. That creates more allocation pressure than the numeric combine paths because intermediate strings are also created.

This only matters if string attributes become common and modifier-heavy. If string attributes are rare or mostly static, the current implementation is fine.

Possible optimization:

- use `StringBuilder`

Trade-off:

- more complexity
- lower readability
- likely unnecessary unless profiling says string modifiers are hot

### 4. Subscription paths allocate delegates and tokens

The notifier and subscription APIs allocate in a few places:

- delegate combination in `AttributeNotifier`
- adapter lambdas for typed subscription helpers
- `IDisposable` token objects

This is normal and probably acceptable because subscriptions are usually created at spawn time or initialization time rather than every frame.

Trade-off of optimizing this:

- custom subscriber storage can reduce allocations
- but it adds implementation complexity and makes the API internals harder to maintain

This is not a priority unless subscriptions are being created and torn down at high frequency.

### 5. `ClearModifiers()` allocates a temporary list snapshot

`ClearModifiers()` snapshots modified keys into a new list before recalculating.

This is correct and safe. The allocation is unlikely to matter unless large-scale modifier clearing happens frequently.

Trade-off of optimizing this:

- list pooling or reusable scratch collections reduce allocations
- but they add bookkeeping and complexity to a path that is probably cold

This is a low-priority optimization.

### 6. The bag chain itself is not the main performance problem

The lookup chain is shallow and dictionary-backed. In the current design, the major costs are not:

- parent traversal
- dictionary lookup count

The more meaningful costs are object creation around keys and recomputed values.

That means the structure is already reasonably efficient where it matters most: unmodified reads do not duplicate data and do not trigger modifier recomputation.

## Trade-Off Summary

### Low-risk improvements

- cache `Attribute` keys at hot call sites
- fix documentation drift
- optionally cache resolved keys inside modifier entries when appropriate

These changes improve performance or clarity without widening the architectural surface area much.

### Medium-risk improvements

- optimize `StringAttributeValue.Combine()`
- reduce allocation in modifier clear/recompute helpers
- replace delegate-combine subscription storage with custom collections

These can help, but they make internals more specialized and slightly harder to maintain.

### High-risk or design-expanding improvements

- move from name-based identity to stable serialized IDs
- change `Attribute` from an allocating record class to a struct or numeric key
- reuse mutable effective value instances
- flatten the bag chain into a fully resolved per-instance table

These would improve pattern purity or runtime performance, but they come with real trade-offs:

- larger migration cost
- more API surface or conceptual complexity
- more hidden state and mutation risk
- lower readability for day-to-day use

## Final Verdict

The package is in good shape structurally.

Its strongest qualities are:

- clear authored-versus-runtime ownership
- a clean three-layer lookup model
- no unnecessary duplication for unmodified attributes
- a readable modifier application pipeline

Its most important weaknesses are:

- unstable runtime identity due to name-based keys
- avoidable key allocations at hot call sites
- recomputed effective values allocating new objects during modifier churn
- a few documentation mismatches that reduce clarity

If the goal is to improve the system without harming readability, the best next step is to keep the architecture intact and focus on:

1. key caching in hot paths
2. documentation cleanup
3. profiling before any deeper allocation-oriented redesign

If the goal is stricter flyweight behavior, the first meaningful redesign would be stable attribute identity rather than micro-optimizing bag internals.
