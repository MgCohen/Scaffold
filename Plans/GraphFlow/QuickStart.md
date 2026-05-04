# GraphFlow Quick-Start

Snapshot 2026-05-04. Reference samples: `Assets/Packages/com.scaffold.graphflow/Samples/M0Sandbox/` and `Samples/CardSandbox/`.

---

## Step 1 — Create folder structure

```
Assets/MyGame/
├── Runtime/
└── Editor/
```

## Step 2 — Create runtime asmdef

In `Runtime/`, right-click → Create → Assembly Definition → name it `MyGame.Runtime`.

Add references (paste GUIDs):
- `bab7af28f08a411c9af5ef2f6d191216` (Scaffold.GraphFlow runtime)
- `b13665e99cedac1faa8a90387e6163c3` (Scaffold.GraphFlow.PackageAttributes)

## Step 3 — Create editor asmdef

In `Editor/`, right-click → Create → Assembly Definition → name it `MyGame.Editor`.

Add references:
- `bab7af28f08a411c9af5ef2f6d191216` (runtime)
- `b13665e99cedac1faa8a90387e6163c3` (attributes)
- `78d83f106afb440f84be7f3da9e310e9` (Scaffold.GraphFlow.Editor)
- By name: `Unity.GraphToolkit.Editor`, `Unity.GraphToolkit.Common.Editor`
- Include Platforms: **Editor** only

Also add a reference from `MyGame.Editor` to `MyGame.Runtime`.

## Step 4 — Create the runner

In `Runtime/MyRunner.cs`:

```csharp
using Scaffold.GraphFlow;

namespace MyGame
{
    public sealed class MyRunner : GraphRunner { }
}
```

Add services as plain properties when you need them. No per-run state here.

## Step 5 — Create the graph asset

In `Runtime/MyGraphAsset.cs`:

```csharp
using Scaffold.GraphFlow;

namespace MyGame
{
    public sealed class MyGraphAsset : GraphAsset<MyRunner> { }
}
```

> The class name **must** be `<RunnerStem>GraphAsset` where stem = runner name minus "Runner". `MyRunner` → `MyGraphAsset`. The generated importer references this exact name.

## Step 6 — Declare the package (twice)

Create `Runtime/AssemblyInfo.cs`:

```csharp
using Scaffold.GraphFlow;
using MyGame;

[assembly: GraphPackage(
    Runner = typeof(MyRunner),
    Extension = "mygraph",
    AssetMenu = "MyGame/My Graph",
    RegistryNamespace = "MyGame.Generated")]
```

Create `Editor/AssemblyInfo.cs` with **identical content**.

> **Decision — Mode 1 or Mode 2?** Skip this if you only have self-executing actions. Add `DispatcherBase = typeof(MyDispatcher<,>), CommandBase = typeof(MyCommand<>)` to **both** declarations if you want command/result Mode 2 (see step 9).

## Step 7 — Wait for compile

Source gen runs. Unity console shows no errors. Inspect via `dotnet run --project Generators/Scaffold.GraphFlow.PackageGenerator.SnapshotTests` if you want to see what got emitted.

## Step 8 — Author payloads

### Imperative entry (host calls it by code)

`Runtime/Entries/OnPlay.cs`:

```csharp
using Scaffold.GraphFlow;

namespace MyGame
{
    [GraphEntry]
    public sealed class OnPlay : IGraphEntry
    {
        [GraphPort] public int CardId;
    }
}
```

### Trigger event (graph subscribes via OnTrigger)

`Runtime/Events/DamageDealt.cs`:

```csharp
using Scaffold.GraphFlow;

namespace MyGame
{
    [GraphEvent]
    public sealed class DamageDealt
    {
        [GraphPort] public int Amount;
    }
}
```

> Don't write a trigger entry class. Built-in `OnTrigger<TEvent>` handles all events; user picks the type from a dropdown in the editor.

### Mode 1 action

`Runtime/Actions/LogAction.cs`:

```csharp
using System.Threading.Tasks;
using Scaffold.GraphFlow;

namespace MyGame
{
    public sealed class LogAction : IGraphAction<MyRunner>, IExecutable<MyRunner>
    {
        [GraphPort] public string Message = "";

        public Task Execute(MyRunner runner)
        {
            // do work
            return Task.CompletedTask;
        }
    }
}
```

> **Decision — Mode 1 or Mode 2?**
> - **Mode 1** (`IExecutable`): the action does its own work, no typed result. Use this when the action is self-contained.
> - **Mode 2** (Command + Dispatcher base): action produces a typed result that downstream nodes can read via output ports. Use this when graphs need to inspect the result.

## Step 9 — (Mode 2 only) Define command + dispatcher bases

Skip if Mode 1. Otherwise:

`Runtime/MyCommand.cs`:

```csharp
using System.Threading.Tasks;
using Scaffold.GraphFlow;

namespace MyGame
{
    public abstract class MyCommand<TResult>
    {
        public abstract Task<TResult> Execute(MyRunner runner, Flow flow);
    }
}
```

`Runtime/MyDispatcher.cs`:

```csharp
using System.Threading.Tasks;
using Scaffold.GraphFlow;

namespace MyGame
{
    public abstract class MyDispatcher<TCmd, TResult> : RuntimeNode<MyRunner>
        where TCmd : MyCommand<TResult>, new()
    {
        public sealed override async Task Execute(MyRunner runner, Flow flow)
        {
            var cmd = BuildPayload();
            var result = await cmd.Execute(runner, flow);
            WriteOutputs(result);
            await flow.GoTo("FlowOut");
        }

        protected abstract TCmd BuildPayload();
        protected abstract void WriteOutputs(TResult result);
    }
}
```

Update **both** `AssemblyInfo.cs` files — add `DispatcherBase = typeof(MyDispatcher<,>), CommandBase = typeof(MyCommand<>)` to the `[GraphPackage]` declaration.

Now write commands as plain types:

```csharp
public sealed class DealDamage : MyCommand<DamageResult>
{
    [GraphPort] public int Amount;

    public override Task<DamageResult> Execute(MyRunner runner, Flow flow) { /* ... */ }
}

public sealed class DamageResult
{
    [GraphPort] public int ActualDamage;
}
```

> Source gen finds any class extending `MyCommand<T>` and emits a closed `MyDispatcher<DealDamage, DamageResult>` automatically. You don't write per-command dispatchers.

## Step 10 — Wait for compile again

Unity recompiles. Source gen emits the editor mirrors + registry + importer.

## Step 11 — Create a graph asset

In Project view, right-click in any folder → Create → MyGame → My Graph (path matches `AssetMenu`). New `.mygraph` file appears.

## Step 12 — Author visually

Double-click the `.mygraph`. Graph editor opens.

Right-click canvas → Create Node. Picker shows:
- Your entries / actions / commands.
- `OnTrigger` (after placing, set the event type + Timing in its inspector dropdowns).
- Built-ins: `Branch`, `Cancel`, `Return<T>`, `Not`.

Drag from arrowhead-out to arrowhead-in for flow. Drag between data ports for values.

Ctrl+S saves. Importer bakes a sub-asset into the same `.mygraph` file.

## Step 13 — Use it at runtime

```csharp
var asset = AssetDatabase.LoadAssetAtPath<MyGraphAsset>("Assets/MyGame/Graphs/MyCard.mygraph");
var runner = new MyRunner();
var controller = new GraphController<MyRunner>(asset);
controller.Initialize(runner);

var flow = await controller.Run(new OnPlay { CardId = 42 });

if (flow.Outcome == FlowOutcome.Returned)
    var v = flow.ReadResult<int>();
```

## Step 14 — (Triggers only) Wire to your event bus

Pattern-match `controller.EntryNodes`:

```csharp
foreach (var entry in controller.EntryNodes)
{
    if (entry is OnTrigger<DamageDealt> trig)
        myBus.Subscribe<DamageDealt>(
            evt => trig.Run(new OnTrigger<DamageDealt> { Event = evt, Timing = trig.Timing }),
            trig.Timing);
}
```

> Read `trig.Timing` (Before / After) before subscribing if your bus distinguishes phases.

---

## Common pitfalls

- **Editor asmdef missing** → no picker entries. Always have both asmdefs.
- **`[GraphEntry]` missing on payload** → not discovered. `IGraphEntry` alone isn't enough.
- **`[GraphPort]` missing on a field** → field is runtime-only, not editable in graph.
- **Asset name doesn't match `<Stem>GraphAsset`** → bake silently produces nothing.
- **EFG-V03 "unknown port"** → field has `[GraphPort]` but you renamed it without rebaking. Re-save the graph.

## What's not yet shipped

- Constant-int / constant-string nodes (to set literals on dispatcher input ports without hand-authoring).
- `Connection.Bind` type-conversion (int↔float etc).
- `TypeReference` dropdown for OnTrigger's event picker (uses string AQN today).
- Display names / categories / port labels in the picker (raw type names today).
