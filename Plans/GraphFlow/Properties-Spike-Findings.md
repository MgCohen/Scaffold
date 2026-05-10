# Properties Spike — Findings

Status: **spike concluded.** Captures what we learned from the
sandbox at `Assets/Sandbox/PropertiesSpike/` (which gets deleted —
this doc is the surviving artifact). Pair with
`Properties-Spike-Plan.md` (the questions) and
`Properties-Migration-Sketch.md` (the original direction this spike
was meant to validate).

## Verdict

**Don't migrate per-tick port binding to `com.unity.properties`.** The
runtime hot path stays on `Scaffold.GraphFlow.PackageGenerator`
source-generated wiring.

Unity.Properties remains a sensible tool for **editor inspection,
runtime UI binding, and generic serialization** — i.e. anywhere
generality beats per-cycle cost. None of those are the port-bind
path.

This is the same architectural line Unity itself draws inside DOTS
(see § "External research" below).

## Spike answers

The three questions from the plan:

| Question | Result | Notes |
| --- | --- | --- |
| **Q-PICKUP** — generator emits a usable bag? | ✅ Pass | `[GeneratePropertyBag]` + `[assembly: GeneratePropertyBagsForAssembly]` produces typed source-gen bags. `PropertyBag.GetPropertyBag<T>()` is the lookup. |
| **Q-ATTRS** — custom attributes survive? | ✅ Pass | `IProperty.HasAttribute<InAttribute>()` etc. round-trip cleanly. `[In]/[Out]/[GraphPort]/[GraphPortIgnore]` all readable. |
| **Q-PERF** — visitor bind ≤ 2× hand-rolled? | ❌ Fail | Best PropertyBag approach (cached delegates after visitor walk) lands at **~4×** hand-rolled. Raw visitor every call is **~480×**. See benchmarks below. |
| Q-COEXIST (secondary) | n/a | Not pursued — Q-PERF failure killed the migration. |

Pass/fail criteria from the plan declared Q-PERF a kill switch if
ratio > 5× **or** non-zero per-tick allocs. We're under 5× on the
cached path (with zero allocs), but still well above the 2× budget
the plan asked for — and crucially, **the cached-delegate approach
is the floor**, not a starting point we can optimize further while
staying on the Property API.

## Benchmarks (EditMode-Mono, 20 samples × 10 000 iter)

Three fields tagged `[In]` / `[In]` / `[Out]` on a class payload
(`int Health`, `string Name`, `Vector3 Position`). Four implementations
of the per-tick copy:

| Approach | Median | vs Hand | Allocs/op |
| --- | --- | --- | --- |
| **Hand** — direct field assignments | 13.5 ns | 1× | 0 |
| **PropertyBag** — visitor at bind, cached `Action<T,T>[]` at runtime | 52.5 ns | **3.9×** | 0 |
| **Raw GraphPorts** — `InputPort.Read` → `OutputPort.Read` → `Flow.ReadCached` | 466 ns | 34.5× | **2** |
| **Mixed** — PropertyBag discovery → GraphFlow ports | 595 ns | 44.1× | 2 |

Bind cost (one-time, per benchmarked binder):

| Approach | Median |
| --- | --- |
| Hand | 0 (compile-time) |
| GraphPorts (manual wiring) | 734 ns |
| PropertyBag (visitor walk + delegate capture) | 5,314 ns |
| Mixed (PropertyBag walk + GraphFlow ports) | 6,428 ns |

The cached-delegate PropertyBag binder is what
`Assets/Sandbox/PropertiesSpike/SpikePortBinder.cs` became at the end
of the spike. The 4× floor reflects the inherent cost of:

- 1 delegate invocation per field
- 2 virtual calls per field (`property.GetValue` / `property.SetValue`)
- 1 cast round-trip per field (concrete `SpikeEvent` ↔ generic `TContainer`)

No code path we tried — pattern-match casts, expression-block delegates,
typed copier interfaces — closed the gap. **Compiled expressions
(`System.Linq.Expressions`) could approach hand-rolled at runtime, but
that path doesn't use the Property API for anything except attribute
discovery — at which point it isn't really a "PropertyBag" approach
anymore, it's reflection-discovery + expression-compile.**

## External research

Two independent agent passes confirmed the boundaries:

### General Unity.Properties usage

- Unity.Properties is **primarily editor-tooling / serialization /
  data-binding substrate**, not a hot-path runtime API.
- First-party production users: Inspector, UI Toolkit runtime bindings,
  `com.unity.serialization`. UI Toolkit's binding system is the closest
  thing to a "runtime hot path" user and **has documented GC complaints
  per-frame** ([forum thread](https://discussions.unity.com/t/ui-toolkit-binding-system-causing-large-gc-allocations/1697259):
  a Unity engineer admitted "building long property paths" allocates).
- No `PropertyContainer.Transfer` / `Copy` / `CopyFrom` API exists.
  Copying values between two instances requires hand-writing a
  `PropertyVisitor` — exactly what the spike did.
- Unity's own docs ([Properties 2.1
  manual](https://docs.unity3d.com/Packages/com.unity.properties@2.1/manual/index.html))
  warn that reflection-based bag creation is "quite slow the first time"
  and recommend **pre-warming the cache during initialization, not in
  `Update()`**.

### DOTS / Entities

The most performance-critical Unity codebase draws the boundary
explicitly:

| Path | Mechanism |
| --- | --- |
| Chunk iteration / `IJobChunk` / Burst | **`TypeManager` only** — never Properties |
| Managed component clone / equals / remap / serialize | **Properties** (already off-Burst, already slow) |
| Editor inspectors, hierarchy, authoring/baking, subscene serialization | **Properties heavily** |

The `Unity.Entities` runtime asmdef references `Unity.Properties`, but
the only runtime usage is in `Unity.Entities/Properties/` for managed
(reference-type) component handling — paths that already pay the
managed-runtime tax and are excluded from Burst by definition. Chunk
iteration uses `TypeManager` and never touches Properties.

Sources verified:
- `Unity.Entities/Unity.Entities.asmdef`
- `Unity.Entities/Properties/ManagedObjectClone.cs`,
  `ManagedObjectBlobs.cs`, `ManagedObjectEquals.cs`,
  `ManagedObjectRemap.cs`
- [TypeManager API docs](https://docs.unity3d.com/Packages/com.unity.entities@1.0/api/Unity.Entities.TypeManager.html)
- [Property bags manual (Unity 6)](https://docs.unity3d.com/6000.3/Documentation/Manual/property-bags.html)

## Recommendation

For GraphFlow:

- **Production runtime (port wiring + per-tick reads)**: keep
  `Scaffold.GraphFlow.PackageGenerator`. Don't add Properties.
- **Editor / debug / serialization** (future): Properties is the right
  tool if we need custom inspectors that show port values, runtime UI
  binding to graph state, or generic graph snapshot/replay. Off the
  hot path.
- **Foreign-type ports** (fields whose type lives outside our
  source-gen catalog — `Transform`, third-party types, etc.): a
  PropertyBag-backed `PropertiesPort<TContainer, TValue>` fallback is
  a reasonable future extension. Same overhead as the spike's
  cached-delegate binder, scoped to foreign-type fields only.

## Adjacent finding worth its own track

The `Raw GraphPorts` 34.5× / 2-allocs-per-op number was the surprise
of the spike. **It has nothing to do with Properties.** The cost
lives in `Flow._cache : Dictionary<Port, object?>` — boxing of value
types and dict-lookup overhead per port read.

This finding seeded `PortCache-Migration-Proposal.md` (separate doc),
which proposes a typed `Entry[]` per-port cache indexed by `flow.Index`
with a global-monotonic version counter for invalidation. Sandbox spike
confirmed 7.6× faster cache-miss path, 4.5× faster cache-hit path, zero
allocs. That proposal stands on its own and would benefit production
runtime regardless of any Properties decision.

## Files

The spike sandbox is at `Assets/Sandbox/PropertiesSpike/` and contains
the Day-1 `[GeneratePropertyBag]` types + attribute-survival tests. The
Day-2/Day-3 implementation (the `SpikePortBinder` cached-delegate
visitor, the 4-way `PerfTest`, the `PortCacheSpike` sandbox that
benchmarked the alternative cache shape) lived in the working tree
during the spike and was removed when the spike concluded — this doc
captures the numbers and decisions so they aren't lost.
