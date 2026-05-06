# GraphFlow Phase-2 Fixes

> **Status: applied.** See `GraphFlow-Audit-Phase2.md` § Resolution log
> for the final disposition of each item, plus deviations from the
> original plan (NS3 stayed as a `get; set;` with a doc comment because
> the package generator's bake-time write blocks `init`-only; SM5
> intentionally skipped; SM6 reverted from `ConditionalWeakTable` to the
> original `Dictionary` after review).
>
> The patches below are the *original* fix specs as proposed. Kept as a
> historical record of the planned shape.

Concrete patches for each finding in `GraphFlow-Audit-Phase2.md`. Apply
in the order listed — NS4 first (biggest correctness win, foundation for
the others), then NS1, then NS2/NS3, then NS5/NS6, then H1, then minor
smells.

Each fix is self-contained: file paths, full replacement code, and any
consumer-side changes needed. No fix touches more than four files.

---

## P0 — NS4 / CC1 / H2 / H3: move `OutputPort` cache to `Flow`

**Why first:** locks in the concurrent-runs invariant. Eliminates the
shared-mutable-state hazard, removes per-port `Dictionary` allocation,
drops the `cache: false` allocation waste, and shrinks the `Port` base.

### `Runtime/Ports/OutputPort.cs` — replace with

```csharp
#nullable enable
using System;

namespace Scaffold.GraphFlow
{
    public sealed class OutputPort<T> : Port
    {
        readonly Func<Flow, T> _compute;
        readonly bool _shouldCache;

        public OutputPort(Func<Flow, T> compute, bool cache = true)
        {
            _compute = compute;
            _shouldCache = cache;
        }

        public T Read(Flow flow) =>
            _shouldCache ? flow.ReadCached(this, _compute) : _compute(flow);
    }
}
```

### `Runtime/Flow/Flow.cs` — replace `_touched`/`RegisterTouched`/`InvalidateAll` block with

```csharp
Dictionary<Port, object?>? _cache;

internal T ReadCached<T>(Port port, Func<Flow, T> compute)
{
    _cache ??= new();
    if (_cache.TryGetValue(port, out var v)) return (T)v!;
    var fresh = compute(this);
    _cache[port] = fresh;
    return fresh;
}

public void InvalidateAll() => _cache?.Clear();
```

Delete: `readonly List<Port> _touched = new();`,
`internal void RegisterTouched(Port p) => _touched.Add(p);`,
the `foreach (var p in _touched) p.ClearCache(this);` block.

### `Runtime/Ports/Port.cs` — replace with

```csharp
#nullable enable
using System;

namespace Scaffold.GraphFlow
{
    public abstract class Port
    {
        internal virtual void ConnectFrom(Port output) =>
            throw new InvalidOperationException(
                $"ConnectFrom is only valid on InputPort<T>; got {GetType()}.");
    }
}
```

(Drop the `ClearCache` virtual entirely.)

### Cost we accept

Value-type cached results (`OutputPort<int>`, `<bool>`, `<float>`) box on
cache miss (one alloc), unbox on hit (no alloc). Bounded per run. If
profiling later flags this, flip cheap pure built-ins (`Add`, `Subtract`,
`GreaterThan`…) to `cache: false` so the cache surface goes away for them.
Don't do that preemptively.

### Validation

- Build the runtime asm — should compile clean.
- Run `Scaffold.GraphFlow.Tests` — `Loop`'s `flow.InvalidateAll()` between
  iterations must still cause `Count.Read(flow)` to recompute.
- Smoke: two concurrent `runner.Run` calls against the same runner with
  different payloads should produce independent results (they couldn't
  before).

---

## P1 — NS1: resolve entry default flow-out at Build time

**Why:** kills the lazy first-run write to a shared field. After this,
`EntryRuntimeNodeBase` has zero mutable runtime state.

### `Runtime/Nodes/EntryRuntimeNode.cs` — replace `EntryRuntimeNodeBase` with

```csharp
[Serializable]
public abstract class EntryRuntimeNodeBase : RuntimeNode
{
    public abstract Type PayloadType { get; }

    FlowOutPort _defaultOut = null!;
    public FlowOutPort GetDefaultOut() => _defaultOut;

    internal override void Build(in NodeBuildSlice slice)
    {
        base.Build(slice);

        FlowOutPort? found = null;
        int count = 0;
        foreach (var p in Ports.Values)
        {
            if (p is not FlowOutPort fo) continue;
            count++;
            found = fo;
        }

        if (count == 0)
            throw new InvalidOperationException(
                $"Entry {GetType().Name} has no FlowOutPort — cannot dispatch via Run<TEntry>.");
        if (count > 1)
            throw new InvalidOperationException(
                $"Entry {GetType().Name} has {count} FlowOutPorts — use RunFromFlowOut to pick one.");

        _defaultOut = found!;
    }
}
```

Drop `_defaultOutResolved`. Drop the lazy block in the old `GetDefaultOut`.

### Validation

The validation that previously fired on first dispatch now fires at
`GraphTopology.Bake` — earlier failure, same error message. Existing
tests that exercise `Run<TEntry>` should pass unchanged.

---

## P2 — NS3: harden `OnTrigger.Timing` as init-only

**Why:** `Timing` is bake-time config (allowed by the rule). The fix is
to encode "config, not state" in the type so a future caller can't write
it at runtime.

### `Runtime/Nodes/OnTrigger.cs` — change line 10

```csharp
public Timing Timing { get; init; }
```

### `Runtime/Markers/IOnTrigger.cs` — replace with

```csharp
namespace Scaffold.GraphFlow
{
    public interface IOnTrigger
    {
        Timing Timing { get; }
    }
}
```

### Validation

- `new OnTrigger<DamageDealt> { Timing = Timing.Before }` still compiles
  (object initializer hits `init`).
- Any code that does `bakedNode.Timing = X` outside an initializer now
  fails to compile. There shouldn't be any — grep `\.Timing\s*=` in
  `Assets/` to confirm.
- Unity's SerializeReference path uses field-level reflection (not the
  property setter), so deserialization is unaffected.

---

## P2 — NS2: drop `Inner` from baked `OnTrigger`, change payload type

**Why:** `Inner` is per-run data (the dispatched event) sitting on a
shared baked node. Today it's saved by the convention of constructing a
fresh `OnTrigger<TEvent>` per dispatch as the payload — but the
type's shape invites the bug. Fix structurally: the baked node carries
ports + `Timing` only; the payload becomes the event itself.

### `Runtime/Nodes/OnTrigger.cs` — replace with

```csharp
#nullable enable
using System;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public sealed class OnTrigger<TEvent> : EntryRuntimeNode<TEvent>, IOnTrigger
        where TEvent : class
    {
        public Timing Timing { get; init; }

        public FlowOutPort FlowOut = null!;
        public OutputPort<TEvent> Event = null!;

        public OnTrigger()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Event = new OutputPort<TEvent>(flow => flow.GetPayload<TEvent>()!);
            Ports.Add(FlowOut.Name, FlowOut);
            Ports.Add(nameof(Event), Event);
        }
    }
}
```

Note the base changed from `EntryRuntimeNode<OnTrigger<TEvent>>` to
`EntryRuntimeNode<TEvent>`. `Inner` is gone.

### Consumer-side update — `Samples/CardSandbox/Runtime/Cards/PlusOneDamage.cs`

The `BuildAsset` constructor for the entry already uses object-initializer
`Timing = Timing.Before` — no change there. Whatever code dispatches the
trigger changes from:

```csharp
runner.Run<OnTrigger<DamageDealt>>(new OnTrigger<DamageDealt> { Inner = evt });
```

to:

```csharp
runner.Run<DamageDealt>(evt);
```

Grep `runner.Run<OnTrigger<` and `\.Inner\s*=` across `Assets/` and
`Samples~/` to find every callsite.

### Catalog/editor side

`OnTriggerEditorNode<TEnum>` and the package-generated catalog may
reference `OnTrigger<TEvent>` as a payload type. Verify:

- `Assets/Packages/com.scaffold.graphflow/Editor/Nodes/OnTriggerEditorNode.cs`
  — should still close the generic over the event enum; no change to the
  shim shape.
- Package generator (`Generators/Scaffold.GraphFlow.PackageGenerator/`)
  — `EntriesByPayload` keys now resolve as `typeof(TEvent)` rather than
  `typeof(OnTrigger<TEvent>)`. The generator's catalog discovery should
  follow `EntryRuntimeNode<T>.PayloadType`, which it already does. No
  generator code change expected; rebuild and verify the snapshot tests
  pass.

If the generator has a hardcoded `OnTrigger<` lookup anywhere, follow the
GraphFlow-Audit constraint in `CLAUDE.md` and rebuild + redeploy the DLL.

### Validation

- `Strike500Tests` and any sample that triggers an event should still
  exercise the event flow.
- `EntriesByPayload` collisions: today, two `OnTrigger<DamageDealt>` nodes
  with different `Timing`s already collide on `typeof(OnTrigger<DamageDealt>)`
  (last-write-wins). After this fix they collide on `typeof(DamageDealt)`.
  Same constraint, different key. Document if it's not already documented.

---

## P1 — NS5: drop runner state in `MySmokeRunner`, inject a sink

**Why:** sample teaches "stash per-run state on the runner" — wrong shape.
Replace with an explicit sink.

### `Samples~/M0Sandbox/Runtime/Smoke/MySmokeRunner.cs` — replace with

```csharp
using System.Collections.Generic;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Smoke
{
    public interface IGraphLogSink
    {
        void Record(string message);
    }

    public sealed class CollectingLogSink : IGraphLogSink
    {
        readonly List<string> _messages = new();
        public IReadOnlyList<string> Messages => _messages;
        public void Record(string message) => _messages.Add(message);
    }

    public sealed class MySmokeRunner : GraphRunner
    {
        public IGraphLogSink LogSink { get; }

        public MySmokeRunner(BakedGraph baked, IGraphLogSink logSink) : base(baked)
        {
            LogSink = logSink;
        }
    }

    public sealed class MySmokeBuilder : GraphBuilder<MySmokeRunner>
    {
        readonly IGraphLogSink _logSink;
        public MySmokeBuilder(IGraphLogSink logSink) { _logSink = logSink; }
        protected override MySmokeRunner CreateRunner(BakedGraph baked) =>
            new(baked, _logSink);
    }
}
```

### Update any dispatcher in the sample that called `runner.RecordLog(msg)`

```csharp
Runner(flow).LogSink.Record(msg);
```

### Validation

Smoke test instantiates `var sink = new CollectingLogSink();`, passes it
to the builder, runs the graph, asserts `sink.Messages` contents. Two
concurrent runs both append to the same sink — the sink is explicitly
cross-run by design.

If you want strict per-run isolation, scope the sink via Flow:
`flow.SetSlot<IGraphLogSink>(LogSlot.Key, perRunSink);`. Not needed for
the smoke sample.

---

## P1 — NS6: drop runner state in `TestRunner`

**Why:** identical pattern in the test fixture; same fix.

### `Tests/Fixtures.cs` — replace `TestRunner` and `TestBuilder` with

```csharp
public interface IGraphLogSink
{
    void Record(string message);
}

public sealed class CollectingLogSink : IGraphLogSink
{
    readonly List<string> _messages = new();
    public IReadOnlyList<string> Messages => _messages;
    public void Record(string message) => _messages.Add(message);
}

public sealed class TestRunner : GraphRunner
{
    public IGraphLogSink LogSink { get; }
    public TestRunner(BakedGraph baked, IGraphLogSink logSink) : base(baked)
    {
        LogSink = logSink;
    }
}

public sealed class TestBuilder : GraphBuilder<TestRunner>
{
    readonly IGraphLogSink _logSink;
    public TestBuilder(IGraphLogSink logSink) { _logSink = logSink; }
    protected override TestRunner CreateRunner(BakedGraph baked) =>
        new(baked, _logSink);
}
```

### Update `TestLogDispatcherRuntime.FlowIn`

```csharp
FlowIn = FlowInPort.Sync(this, nameof(FlowIn), flow =>
{
    Runner(flow).LogSink.Record(Message.Read(flow) ?? "");
    return FlowOutPort.End;
});
```

### Update tests

```csharp
var sink = new CollectingLogSink();
var builder = new TestBuilder(sink);
var runner = builder.Build(asset);
await runner.Run(payload);
Assert.AreEqual("expected", sink.Messages[^1]);
```

### Note on `IGraphLogSink` placement

Don't promote `IGraphLogSink` to the runtime asm — logging isn't a
framework concept, it's an example of what a sample dispatcher does.
Define the interface separately in the sample and in the test asm; the two
copies are 6 lines each. Avoids leaking sample API into runtime.

---

## P1 — H1: switch `FlowInPort.Invoke` to `ValueTask`

**Why:** every sync node fire allocates a fresh `Task<FlowOutPort>` via
`Task.FromResult`. With ValueTask, sync paths are zero-alloc.

### `Runtime/Ports/FlowInPort.cs` — replace with

```csharp
#nullable enable
using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public sealed class FlowInPort : Port
    {
        public RuntimeNode Owner { get; }
        public string Name { get; }
        public FlowConnection? Connection { get; internal set; }
        internal Func<Flow, ValueTask<FlowOutPort>> Invoke { get; }

        FlowInPort(RuntimeNode owner, string name, Func<Flow, ValueTask<FlowOutPort>> invoke)
        {
            Owner = owner;
            Name = name;
            Invoke = invoke;
        }

        public static FlowInPort Sync(
            RuntimeNode owner, string name, Func<Flow, FlowOutPort> handler) =>
            new(owner, name, flow => new ValueTask<FlowOutPort>(handler(flow)));

        public static FlowInPort Async(
            RuntimeNode owner, string name, Func<Flow, Task<FlowOutPort>> handler) =>
            new(owner, name, flow => new ValueTask<FlowOutPort>(handler(flow)));
    }
}
```

### `Runtime/Controller/GraphRunner.cs` — `RunFromInPort` becomes

```csharp
async Task RunFromInPort(FlowInPort start, Flow flow)
{
    FlowInPort? current = start;
    while (current != null)
    {
        var next = await current.Invoke(flow).ConfigureAwait(false);
        if (ReferenceEquals(next, FlowOutPort.End)) break;
        current = next.Connection?.Destination;
    }
}
```

(No body change — `await` works on `ValueTask<T>` identically.)

### Validation

`Wait` (the only async built-in) still allocates a `Task` for `Task.Delay`
— the wrap into `ValueTask<>` is free. Sync nodes (`Branch`, `Cancel`,
`Loop`, `Add`, …) no longer allocate per fire. Run a quick GC.GetTotalMemory
delta around a tight sync graph to confirm.

---

## P3 — minor smells (apply if convenient)

### SM2. `FlowOutPort.End` sentinel with `null!` Owner

Replace the static sentinel pattern with `FlowInPort.Invoke` returning
`FlowOutPort?` (where `null` means "end of flow"). `RunFromInPort`
becomes:

```csharp
while (current != null)
{
    var next = await current.Invoke(flow).ConfigureAwait(false);
    if (next == null) break;
    current = next.Connection?.Destination;
}
```

Drop `FlowOutPort.End`. Update every `return FlowOutPort.End;` in built-ins
to `return null;` and change the `Sync`/`Async` factories to take
`Func<Flow, FlowOutPort?>` / `Func<Flow, Task<FlowOutPort?>>`.

### SM4. Drop `[Serializable]` from `PortMeta`

```csharp
public readonly struct PortMeta { … }
```

(Remove the attribute.) `Type` isn't Unity-serializable; the catalog
holds `PortMeta` in memory only.

### SM5. `Port.ConnectFrom` could be abstract

Trade-off: keeping it virtual-throws keeps the polymorphic
`dst.ConnectFrom(src)` site in `GraphTopology` working. Making it abstract
forces every `Port` subtype to declare intent. Pick one. Both compile;
the abstract form is louder.

### SM6. `GraphBuilder._cache` is unbounded

Document on `GraphBuilder` that it's expected to be one-per-scene, not a
singleton. If a long-lived builder is desired, switch the cache to
`ConditionalWeakTable<GraphAsset, BakedGraph>` so unloaded assets release
their baked graphs.

---

## Order of application & verification

1. **NS4** (cache → Flow). Run all tests.
2. **NS1** (resolve at Build). Run all tests.
3. **NS3** (Timing init-only). Compiles or doesn't — fix any callsite.
4. **NS2** (drop `Inner`, payload becomes `TEvent`). Update callsites; rebuild package generator if needed; run snapshot + runtime tests.
5. **NS5** + **NS6** (sinks). Sample + test changes only; run smoke + tests.
6. **H1** (ValueTask). Run all tests.
7. **SM2 / SM4 / SM5 / SM6** if convenient.

Each step compiles cleanly on its own. Don't bundle into one commit —
keep them separate so the bisect surface stays narrow if anything
regresses.
