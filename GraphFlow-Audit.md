# GraphFlow Architecture Audit

## Critical Issues

### C1. Comments — strip them

Almost every runtime file is 40-60% xml-doc by line count. Comments describe WHAT the code does, reference milestone numbers (`M2`, `post-M3 phase 2 decision #5`), and explain rationale that belongs in commit messages or design docs. `IEffectScope.cs` is 17 lines for an empty interface (16 lines comment, 1 line code).

**Fix:** Strip all xml-docs from runtime types. Keep at most a one-line comment when the WHY is genuinely non-obvious (a hidden constraint, a workaround, a subtle invariant). Delete every "post-M3 decision #N" reference — that's git history, not documentation.

---

### C2. Over-guarding on every constructor

Every internal constructor null-checks its arguments. These are framework-internal types whose callers are generator-emitted code or the framework's own hydration loop — nobody passes null.

**Cases:**

| File | Line | Guard |
|---|---|---|
| `Runtime/Ports/Ports.cs` | 64 | `OutputPort<T>(Func<T> read)` null-checks `read` |
| `Runtime/Ports/Ports.cs` | 87-89 | `FlowOutPort` null-checks `owner` and `name` |
| `Runtime/Ports/Ports.cs` | 106-108 | `FlowInPort` null-checks `owner` and `name` |
| `Runtime/Ports/Connection.cs` | 23-25 | `Connection.Bind` null-checks both ports — and `AcceptOutput` already type-rejects null |
| `Runtime/Controller/GraphController.cs` | 28 | ctor null-checks `asset` |
| `Runtime/Controller/GraphController.cs` | 33 | `Initialize` null-checks `runner` |
| `Runtime/Controller/GraphController.cs` | 99-100 | `Run` checks `_bridges == null` (impossible — initialized inline) |
| `Runtime/Nodes/RuntimeNode.cs` | 43-47 | `Bind` null-checks `src` AND looks up both ports with throwing `TryGetValue` patterns |
| `Runtime/Markers/PortMeta.cs` | 27-29 | ctor null-checks `name` and `type` |
| `Runtime/Markers/CatalogEntry.cs` | 59-61 | ctor null-checks `type` and `ports` |

**Fix:** Remove all `ArgumentNullException` from internal constructors and wiring methods. The only acceptable guard is on `GraphController.Run<TEntry>` where the bridge dispatch is the public entry. Even there, a missing bridge throws naturally.

---

### C3. `GraphController.Dispose` is fake cleanup

```csharp
public void Dispose()
{
    _byId?.Clear();
    _bridges?.Clear();
    _entryNodes?.Clear();
}
```

Doesn't implement `IDisposable`. Doesn't null `_runner`. Doesn't prevent reuse. Calling `.Clear()` on dictionaries you're about to abandon is wasted CPU — the GC handles it.

**Fix:** Delete the method. If a real disposable resource appears later, implement `IDisposable` properly then.

---

## Structural Issues

### S1. `EntryRuntimeNode<TEntry>` double-dispatch redundancy

`EntryRuntimeNode<TEntry>` carries `_runFromHere`, `SetPayload`, and `Run` on the node itself.
`EntryBridge<TEntry, TRunner>` holds 5 fields (node, runner, asset, executor, scopeFactory) and exposes a `Run(object)` that does the same work as `_runFromHere`.

The bridge ctor wires `_runFromHere = payload => { SetPayload; RunFlow; }` — then duplicates that logic in its own `Run(object)`. Two paths to the same code.

**Fix:** Pick one. Either:
- Drop the bridge entirely; let the controller close over the executor/asset/scope factory in a delegate keyed by payload type. The node already has `Run(payload)` — use it.
- Or drop `_runFromHere` from the node; hosts that pattern-match `OnTrigger<T>` go through the controller's bridge dispatch.

The first option removes more code.

---

### S2. `RuntimeNode.Connections` list is write-only

`Runtime/Nodes/RuntimeNode.cs:28` declares `public readonly List<Connection> Connections = new();`. `Bind` appends to it. The comment says "the executor never reads from here." Nothing else reads it either.

**Fix:** Delete the field. Delete the `Connections.Add(conn)` line in `Bind`.

---

### S3. `GraphExecutor<TRunner>` is a stateless instance

`GraphController` holds `readonly GraphExecutor<TRunner> _executor = new()`. The executor has zero state. `RunFlow` takes everything as parameters.

**Fix:** Make `GraphExecutor.RunFlow` a static method on a non-generic helper, or inline it into `GraphController`. Drop the field.

---

### S4. `GraphRunner.CancellationToken` appears unused

`GraphRunner` is a near-empty abstract class with one `CancellationToken` property. The executor reads `ct` from `RunFlow`'s parameter and constructs `Flow(ct)` from it — not from the runner. Search the package: nothing reads `runner.CancellationToken`.

**Fix:** Verify with grep, then delete the property. If it's a planned hook for cancellation, leave a comment. Otherwise the runner becomes a pure marker base — which is fine.

---

### S5. `IEffectScope` marker buys no type safety

`Flow.Scope` is typed as `IEffectScope?`. Every consumer (CardSandbox dispatchers, Strike500Dispatcher, CardCommandDispatcher) does `(ICardEffectScope)flow.Scope!` — an unsafe downcast.

**Fix:** Change `Flow.Scope` to `object?`. Delete `IEffectScope`. The cast is no less safe; the marker interface adds an inheritance hop and a `using` for nothing.

---

### S6. `LifecycleInterfaces.cs` is dead code

`IInitializableNode<TRunner>` and `IListenerNode<TRunner>` — neither is implemented by any node, neither is checked by the controller or executor. Comment says "populated when M2/M3 need init/listeners" — that didn't happen.

**Fix:** Delete the file.

---

### S7. `CrossAsmSpikeNode` is a leftover spike

`Editor/Nodes/CrossAsmSpikeNode.cs` — class comment literally describes itself as a "phase 0.5 spike" testing `[UseWithGraph]` inheritance. Production code is shipping a debug experiment.

**Fix:** Delete the file. The spike's question has been answered (the `OnTriggerEditorNode<TEnum>` shim works).

---

## Generator Issues

### G1. `TrimGlobal` defined three times

Same body in three places:
- `Generators/.../GraphRegistryEmitter.cs:379-382`
- `Generators/.../GraphCatalogEmitter.cs:401-404`
- `Generators/.../GraphCatalogDiscovery.cs:165-166`

**Fix:** Move to a single static helper (e.g. `GraphCompilationNames.TrimGlobal`). Delete the other two.

---

### G2. `IsEditorAssembly` defined twice

- `Generators/.../GraphPackageAssemblyParser.cs:49-53`
- `Generators/.../GraphPackageEmitter.cs:229-233`

Identical implementation.

**Fix:** Move to a shared helper. Delete one copy.

---

### G3. `SafeFieldName` is a no-op

`GraphCatalogEmitter.cs:394-398` — returns `raw` unchanged. Comment admits it's a placeholder.

**Fix:** Inline the calls (just use the type name directly). Delete the method.

---

### G4. `GraphGenericNodeEmitter.EmitRuntimePartial` is dead code

`GraphGenericNodeEmitter.cs:43-100` — fully-formed method, never called. `EmitForPackage` only emits editor mirrors and registration blocks; runtime partials are emitted by the separate `GraphNodeRuntimePartialGenerator`.

**Fix:** Delete the method.

---

### G5. Catalog walks the same assembly four times

`EmitCatalogIfRunnerAsm` calls `GraphCatalogDiscovery.DiscoverEvents/DiscoverCommands/DiscoverEntries/DiscoverReturns` — each one calls `GraphPayloadTypeWalker.AllNamedTypesInAssembly(payloadAsm, ct)` and builds a fresh `ImmutableArray` of every type in the assembly.

**Fix:** Walk once at the top of `EmitCatalogIfRunnerAsm`, pass the cached `ImmutableArray<INamedTypeSymbol>` into each `Discover*` method.

---

### G6. Generated catalog uses LINQ on lookup paths

`GraphCatalogEmitter` emits:
```csharp
public static IEnumerable<CatalogEntry> OfKind(CatalogKind k) =>
    s_All.Where(e => (e.Kinds & k) != 0);
public static CatalogEntry? Get(System.Type t) =>
    s_All.FirstOrDefault(e => e.Type == t);
```

`Get(Type)` is linear-scan with closure allocation. Editor-only, but called per-node during bake.

**Fix:** Emit a static `Dictionary<Type, CatalogEntry>` alongside `s_All` and have `Get` return from it. `OfKind` can stay LINQ — it's iterated, not point-lookup.

---

## Things That Are Good — Keep Enforcing

### G+1. Executor loop is 8 lines
`GraphExecutor.RunFlow` is a tight `while (current != null)` walk. **Why:** No branching on edge metadata, no bookkeeping. Keep it this small.

### G+2. Hydration-once / direct-refs-thereafter
`GraphController.Initialize` resolves all wiring upfront; the executor never reads `asset.flowEdges` or `asset.connections` again. **Why:** Zero allocation, zero metadata lookup on the hot path. Don't introduce runtime metadata lookups.

### G+3. Single cast seam in `InputPort<T>.AcceptOutput`
The whole type system collapses to one virtual call. **Why:** Type conversion (M4 converters) plugs in here once instead of N reflection sites. Don't add casts elsewhere.

### G+4. `Flow` is per-run, never shared
A fresh `Flow` instance per `controller.Run`. Two concurrent runs = two flows, no shared state. **Why:** Async safety without locks. Don't add state to `GraphRunner` that should be per-run.

### G+5. Outcome via four one-line methods on `Flow`
`GoTo` / `Stop` / `Return` / `Cancel` all return `Task.CompletedTask` so a node body is `return flow.GoTo(MyOut);`. **Why:** Authoring stays one-line. Don't add an Execute return type or out-parameters.

### G+6. Two-tier node hierarchy
`RuntimeNode` (runner-agnostic) and `RuntimeNode<TRunner>` (typed). Built-ins (Branch, Cancel, Not, Return) skip TRunner. **Why:** The runner type is only paid for when actually needed. Don't make TRunner mandatory.

### G+7. Built-in nodes are minimal
`Branch` is 2 lines of body. `Cancel`, `Not`, `Return` are similarly tight. **Why:** These set the baseline for what a node should look like. Don't let new built-ins drift heavier.

### G+8. Generator split into two
`GraphPackageIncrementalGenerator` (per-package scaffolding) + `GraphNodeRuntimePartialGenerator` (per-`[GraphNode]` ctor partial, runs on every compilation). **Why:** Lets package built-ins live in an asm without `[GraphPackage]`. Don't merge them.

### G+9. `[GraphNode]` partial-class pattern
Author declares port fields; generator emits the ctor that constructs handles + populates the dict. **Why:** Authors write data, not boilerplate. Don't ask authors to write ctors.

### G+10. Catalog is the single source of truth for editor pickers
`<Stem>Catalog` emits per-concern enums + `Resolve(choice)` resolvers. Editor mirrors read from it. **Why:** No reflection on `Type.GetFields`, no `EventTypeRegistry` indirection. Don't add parallel discovery surfaces.

### G+11. Per-package shim is 5 lines
`OnTriggerEditorNode` and `ReturnEditorNode` shims close the generic over the package's enum and forward to the catalog. **Why:** GraphToolkit needs closed enums at compile time and `[UseWithGraph]` needs a concrete graph type — these are non-negotiable. Keep the shims this thin.

### G+12. Edge format is one shape
`Edge` struct used for both data and flow edges; role determined by which list it sits on. **Why:** No dual serialization layout. Don't split into `DataEdge`/`FlowEdge`.

### G+13. Convention-driven port discovery
`PortConvention.AllFieldsIn` + `[GraphPortIgnore]` opt-out is the package-default; `AttributedFields` is opt-in. **Why:** Default reads as ergonomic; precision available when needed. Don't force attributes everywhere.

### G+14. Symmetry: `Connection<T>` and `FlowConnection`
Both built at hydration, both never serialized, both pair source/dest with back-refs on each endpoint. **Why:** One mental model for wiring. Don't asymmetrize.

### G+15. Tests use `BuildAsset` factories instead of round-tripping the editor
`Strike500Tests` constructs `CardEffectGraphAsset` in code via `Strike500.BuildAsset()`. **Why:** Runtime tests stay independent of the editor importer. Don't couple runtime tests to GraphToolkit.

---

## Priority for Cleanup

1. **C1** — Strip comments (largest line-count win)
2. **C2** — Remove null guards
3. **S6, S7, G4** — Delete dead code
4. **C3, S2, S5** — Delete fake/unused fields and methods
5. **G1, G2, G3** — Deduplicate generator helpers
6. **S1** — Resolve EntryRuntimeNode/EntryBridge duplication
7. **S3, S4** — Static-ify executor, audit `GraphRunner.CancellationToken`
8. **G5, G6** — Generator perf wins (medium priority — editor-only paths)
