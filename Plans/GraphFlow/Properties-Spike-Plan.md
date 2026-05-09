# Properties Spike — Validation Plan

A throwaway sandbox to answer three load-bearing questions from `Properties-Migration-Sketch.md` before committing to the migration. **No production code changes.** Sandbox lives in a self-contained asmdef and gets deleted (or kept as a benchmark) after the spike concludes.

## Questions the spike must answer

The three risks from the sketch, restated as yes/no questions:

1. **Q-PICKUP — Does Unity's source generator emit a usable bag for a hand-annotated type?**
   *Type marked `[GeneratePropertyBag]` + assembly marked `[GeneratePropertyBagsForAssembly]` → `PropertyBag.GetPropertyBag<T>()` returns a non-reflection bag whose properties are typed and complete.*

2. **Q-ATTRS — Does the generated bag preserve user attributes on fields?**
   *`IProperty.HasAttribute<InAttribute>()` / `GetAttribute<GraphPortAttribute>()` return the actual `[In]/[Out]/[GraphPort]/[GraphPortIgnore]` attached to the underlying field.*

3. **Q-PERF — Is the visitor-based port bind path within budget?**
   *Visitor approach is ≤ 2× the cost of hand-rolled `rt.X = v` assignment, with **zero per-tick GC allocations** after warmup.*

A fourth, **secondary** question — useful to know but doesn't gate the migration:

4. **Q-COEXIST — If *our* generator emits `[GeneratePropertyBag]` on a node type, does Unity's generator pick it up in the same compilation?**
   *If no: users (or our generator via partial files) must annotate by hand. Annoying, not fatal.*

## Pass/fail criteria

| Question | Pass | Fail (kills migration?) |
| --- | --- | --- |
| Q-PICKUP | Generated bag exists, `bag.GetProperties()` returns expected count, types match | **Yes** |
| Q-ATTRS  | `HasAttribute<T>` returns true for fields tagged with custom attrs | **Yes** |
| Q-PERF   | ≤ 2× hand-rolled, **0 B/tick allocs** after warmup | **Yes** (if >5× or per-tick allocs) |
| Q-COEXIST | Our gen emits `[GeneratePropertyBag]`, Unity's gen sees it | **No** — falls back to hand-annotation |

If Q-PICKUP, Q-ATTRS, or Q-PERF fail → migration is dead, sketch gets archived. Q-COEXIST result shapes the migration's authoring ergonomics, not its viability.

## Non-goals

- No changes to `Scaffold.GraphFlow.PackageGenerator` (read-only reference).
- No changes to runtime port routing (`PortValueResolver`, `GetInputPortByName`, etc.).
- No editor UI work.
- No test of the Unity 6 data binding path. Pure Properties API.
- No integration with the actual catalog. Standalone fixture.

## Scope of fixture

**Three representative types** (matching the shapes the migration would touch):

1. **`SpikeEvent`** — pure data payload, ~5 fields of mixed types (`int`, `string`, `Vector3`, a custom struct, a reference type), some `[In]`-tagged, some `[Out]`-tagged, one `[GraphPortIgnore]`.
2. **`SpikeCommand`** — `class : CommandBase<SpikeResult>`-ish shape with input + output fields, mirroring how command payloads look today.
3. **`SpikeEntry`** — `IGraphEntry<SpikePayload>`-style class to confirm interface-implementing types still bag cleanly.

These are throwaway types — nothing in the live runtime references them.

## Deliverables

**Code (sandbox, deletable):**
```
Assets/Sandbox/PropertiesSpike/
├── Scaffold.GraphFlow.PropertiesSpike.asmdef     // runtime asmdef
├── SpikeEvent.cs                                  // hand-annotated payloads
├── SpikeCommand.cs
├── SpikeEntry.cs
├── SpikeAttributes.cs                             // [In], [Out], [GraphPort], [GraphPortIgnore] mocks
├── SpikePortBinder.cs                             // PropertyVisitor-based binder
├── AssemblyInfo.cs                                // [assembly: GeneratePropertyBagsForAssembly]
└── Editor/
    ├── Scaffold.GraphFlow.PropertiesSpike.Editor.asmdef
    ├── PickupTest.cs                              // EditMode test for Q-PICKUP
    ├── AttributesTest.cs                          // EditMode test for Q-ATTRS
    ├── PerfTest.cs                                // EditMode perf test for Q-PERF
    └── CoexistProbe.cs                            // optional Q-COEXIST probe
```

**Doc (kept regardless of outcome):**
```
Plans/GraphFlow/Properties-Spike-Findings.md   // results + recommendation
```

**Package addition:**
- `Packages/manifest.json` gains `"com.unity.properties": "<pinned version>"` (latest stable 2.x).

## Experiments

### E1 — Q-PICKUP (½ day)

Hand-annotate `SpikeEvent` with `[GeneratePropertyBag]`, mark assembly with `[GeneratePropertyBagsForAssembly]`. Inspect:

```csharp
var bag = PropertyBag.GetPropertyBag<SpikeEvent>();
Assert.NotNull(bag);
Assert.AreEqual(typeof(ReflectedPropertyBag<SpikeEvent>), bag.GetType()); // expect: NOT this
// expect: a generated type like SpikeEvent_PropertyBag
```

Also: dump the generated source from `Library/Bee/artifacts/` (or wherever Roslyn writes it) into the findings doc.

**Pass:** generated bag, all instance fields enumerable, types correct.

### E2 — Q-ATTRS (½ day)

On `SpikeEvent`, attach mock `[In]`, `[Out]`, `[GraphPort]`, `[GraphPortIgnore]` to specific fields. Walk the bag with a visitor:

```csharp
foreach (var prop in bag.GetProperties())
{
    var hasIn       = prop.HasAttribute<InAttribute>();
    var graphPort   = prop.GetAttribute<GraphPortAttribute>();
    // assert against an expected table
}
```

**Pass:** every attribute is observable on the matching property, exactly once.

### E3 — Q-PERF (1 day)

Two paths, same workload:

- **Baseline (hand-rolled, mirrors today's generator output):**
  ```csharp
  rt.Health = inputs.Health;
  rt.Name = inputs.Name;
  rt.Position = inputs.Position;
  outputs.Damage = rt.Damage;
  outputs.Hit = rt.Hit;
  ```
- **Visitor (the migration target):** `SpikePortBinder.BindInputs(ref rt, inputs)` + `BindOutputs(ref rt, outputs)`, where `SpikePortBinder : PropertyVisitor` does the work via `Property<T,V>.SetValue` / `GetValue`.

Workload: 10,000 iterations × `SpikeEvent` (5 ports) per measurement. Measure:

- Wall time (Unity `PerformanceTesting` or `Stopwatch` × 10 runs, median).
- GC alloc per measurement window (`Recorder.sampleBlockCount` / `GC.GetTotalAllocatedBytes`).
- Run in **Editor (Mono)** AND **standalone Player (IL2CPP)** if buildable; otherwise just Editor with a flagged caveat.

Cache the bag and the `IProperty[]` outside the hot loop — that mirrors what the production binder would do.

**Pass:** median visitor time ≤ 2× baseline, allocations after first iteration are 0 B (visitor instance + property array are both reusable).

If perf fails: profile (`Profiler.BeginSample`) to find the cost — likely candidates are `IPropertyBag.Accept` dispatch, attribute lookup, or boxing in a corner case.

### E4 — Q-COEXIST (½ day, optional)

A tiny standalone Roslyn generator project that emits a partial class with `[GeneratePropertyBag]`. Compile alongside Properties' generator. Inspect whether Properties' generated output mentions the type.

If Roslyn architecture clearly forbids it (incremental generator output isn't an input to other generators), skip the experiment and document the constraint.

**Pass:** Properties' generator picks up our emitted attribute → migration can be fully automated.
**Fail:** users hand-annotate (or our generator emits user-facing partial files they must commit alongside their code).

## Time budget

3 working days, structured as:

- **Day 1** — package install, sandbox scaffold, E1, E2.
- **Day 2** — E3 perf rig, full measurements.
- **Day 3** — E4 (if pursuing), write up findings, decide.

If E1 or E2 fails on Day 1, stop — write up the failure and revisit the sketch.

## Rollback / cleanup

- If migration is greenlit: keep `SpikePortBinder.cs` as the seed for the production binder, delete the rest.
- If migration is shelved: delete the entire `Assets/Sandbox/PropertiesSpike/` folder, revert `Packages/manifest.json` (or keep the package if other features want it). The findings doc stays.

## Things I'm explicitly NOT testing in this spike

- Generic types (`EntryRuntimeNode<TEntry, TRunner>`) — defer until base case works.
- Collection / list ports — defer.
- Cross-assembly bag visibility — assume same assembly for spike.
- Unity Player IL2CPP build only if cheap; otherwise Editor Mono is enough to gate the decision.
- Incremental compile/edit-time UX with the Properties generator — not a blocker to validation.

## Decision gate

After Day 3, the findings doc states: **migrate**, **migrate-with-hand-annotation**, or **shelve**. No further code lands until that decision is in writing.
