# Properties Migration — Sketch

Status: research sketch, not yet a decision. Captures the shape of leaning on `com.unity.properties` (`IPropertyBag<T>`, `Property<T,V>`, `PropertyVisitor`) for all per-type "tumbling" so `Scaffold.GraphFlow.PackageGenerator` can shed its hand-rolled field discovery and per-field plumbing.

## Goal

Invert the responsibility split:

- **Unity Properties owns the type-internal model** — what fields exist, how to read/write them in a typed way, what custom attributes ride on each field.
- **Our generator owns only the cross-type model** — which types are nodes/events/entries/commands/returns, what their kind is, how to dispatch by enum/picker, factory lambdas, editor mirrors.

Net effect: per-field code emission disappears; the generator emits a thin catalog of *types* and lets `IPropertyBag<T>` answer everything else at runtime.

## Current shape (baseline)

`Scaffold.GraphFlow.PackageGenerator` does three structurally different jobs:

1. **Type discovery** — scans the assembly for `[GraphEvent]`, `IGraphEntry`, `CommandBase<T>`, `[GraphReturnType]`, `[GraphNode]`. (`GraphCatalogDiscovery`)
2. **Per-type field tumbling** — walks fields via `IFieldSymbol`, classifies as input/output port using `[In]`/`[Out]`/`[GraphPort]`/`[GraphPortIgnore]` and the assembly's `[GraphPackage]` convention. (`PayloadDiscovery.InstanceFields`, `PayloadDiscovery.IsPortField`, `FieldClassifier`)
3. **Catalog + per-field emission** — emits `<Stem>Catalog.g.cs` (CatalogEntry array, kind enums, `OfKind`, `Resolve`, factory lambdas, `PortMeta[]`) and `<Stem>GraphRegistry.g.cs` (per-field `rt.FieldName = PortValueResolver.TryResolve<T>(...)` blocks). (`GraphCatalogEmitter`, `GraphRegistryEmitter`)

Job (1) is irreducible — it's the cross-type discovery the catalog is built from.
Job (2) is exactly what `IPropertyBag<T>` already models.
Job (3) is half-catalog (cross-type, our concern), half-plumbing (per-field, Properties' concern).

## Target shape

```
Discovery (our generator, Roslyn)
  ─► emits: CatalogEntry array, kind enums, Resolve/OfKind, factory lambdas
  ─► annotates: each discovered type with [GeneratePropertyBag]
  ─► emits: [assembly: GeneratePropertyBagsForAssembly] once

Per-type model (Unity's generator)
  ─► emits: IPropertyBag<TPayload> with strongly-typed Property<TPayload, TField>
  ─► registers in PropertyBag.GetPropertyBag(typeof(T))

Runtime port binding (small static engine, hand-written)
  ─► PropertyVisitor that reads [In]/[Out]/[GraphPort]/[GraphPortIgnore]
     off IProperty.GetAttribute<>(), classifies direction, and bridges
     to/from runtime PortValueResolver
```

Things that **disappear** from our generator:

- `PayloadDiscovery.InstanceFields` / `IsPortField`
- `FieldClassifier`
- The `dataInputs`/`dataOutputs` enumeration in `GraphRegistryEmitter` (lines ~334–348)
- The per-field `rt.FieldName = ...` blocks in registry emit
- The `PortMeta[]` literal baked into each `CatalogEntry` *(see open question 1)*

Things that **stay** in our generator:

- Cross-type discovery and `CatalogEntry` array
- `EventType` / `ReturnType` / `CommandType` / `EntryType` picker enums
- `Resolve(EventType) → CatalogEntry?` switches and `OfKind(CatalogKind)` filter
- Factory lambdas (`CreateRuntime`)
- Editor mirrors (payload nodes, command/result dispatchers)
- Assembly-level `[GraphPackage]` convention reading

## Inversion table

| Concern                                  | Today (our gen)                                     | After migration                                      |
| ---------------------------------------- | --------------------------------------------------- | ---------------------------------------------------- |
| "Which types are graph types?"           | `GraphCatalogDiscovery`                             | unchanged                                            |
| "What fields does this type have?"       | `PayloadDiscovery.InstanceFields`                   | `PropertyBag.GetPropertyBag<T>()` iteration          |
| "Is this field a port? Direction?"       | `IsPortField` + convention switch                   | `IProperty.GetAttribute<InAttribute>()` etc. in visitor |
| "Read input port value into field"       | generated `rt.X = PortValueResolver.TryResolve<T>`  | `Property<T,V>.SetValue(ref c, v)` in visitor        |
| "Write output field value to port"       | generated `port.SetValue(rt.X)`                     | `Property<T,V>.GetValue(ref c)` in visitor           |
| "Catalog of all events / commands / ..." | generated `CatalogEntry[] s_All`                    | unchanged                                            |
| "Picker enums + Resolve switches"        | generated `EventType` / `Resolve(EventType)`        | unchanged                                            |
| "Factory: new TPayload()"                | generated lambda                                    | unchanged                                            |

## Sketch — runtime port binder

A single visitor replaces all per-field generated registry code:

```csharp
internal sealed class GraphPortBinder : PropertyVisitor
{
    private RuntimeNode _node;
    private BindMode _mode; // ReadInputs | WriteOutputs

    public void BindInputs<T>(ref T container, RuntimeNode node) where T : struct
    {
        _node = node; _mode = BindMode.ReadInputs;
        var bag = PropertyBag.GetPropertyBag<T>();
        bag.Accept(this, ref container);
    }

    protected override void VisitProperty<TContainer, TValue>(
        Property<TContainer, TValue> property,
        ref TContainer container,
        ref TValue value)
    {
        if (property.HasAttribute<GraphPortIgnoreAttribute>()) return;

        var direction = ResolveDirection(property); // [In]/[Out]/convention
        if (direction == PortDirection.Input && _mode == BindMode.ReadInputs)
        {
            var port = _node.GetInputPortByName(property.Name);
            if (port != null && PortValueResolver.TryResolve<TValue>(port, out var v))
                property.SetValue(ref container, v);
        }
        else if (direction == PortDirection.Output && _mode == BindMode.WriteOutputs)
        {
            var port = _node.GetOutputPortByName(property.Name);
            port?.SetValue(property.GetValue(ref container));
        }
    }
}
```

This single ~50-line file replaces the per-field generated blocks — the code is no longer N lines × M nodes; it's N lines, period.

## Generator delta

What `GraphRegistryEmitter` shrinks to (per type):

```csharp
[GeneratePropertyBag]                       // ◄── only addition the gen makes
public partial class OnPlay : IGraphEntry<OnPlay> { /* user code */ }

// Generated CatalogEntry — just the cross-type wiring:
new CatalogEntry(
    kind:        CatalogKind.Entry,
    declaredType:typeof(OnPlay),
    createRuntime: () => new OnPlay(),
    resultType:  null,
    defaultLiteral: null
    // no PortMeta[] — derived lazily from PropertyBag if asked
)
```

Compare against today's emit which also includes a `PortMeta[]` literal and (in the registry file) a per-field assignment block.

## Open questions

1. **`PortMeta[]` static array.** Today it's eagerly emitted into the catalog and consumed by the editor for port UI. Options:
   - (a) Keep emitting it from our generator — it's cheap, descriptive, and the editor likes static data. We'd derive it at gen-time by walking `IFieldSymbol`s anyway, so we don't actually shed all per-field gen code, only the runtime registry half.
   - (b) Drop the literal; build it lazily on first access via `PropertyBag.GetPropertyBag(t).GetProperties()`. Costs one walk per type per session. Probably fine.
   - **Lean: (b)** — keeps the inversion clean. Revisit if editor startup regresses.

2. **Coexistence of two source generators.** Unity's Properties generator and ours run in the same compilation. Our generator needs to *add* `[GeneratePropertyBag]` to user types via partial-type emission (or expect users to write it themselves). Investigate whether the Properties generator picks up attributes added by another generator in the same pass — if not, we either ask users to annotate, or pre-bake the annotation into a separate file emitted in an earlier phase.

3. **Properties package maturity.** 2.1.0-**exp**. Used in production by UI Toolkit + Serialization, but the experimental tag means API churn risk. Pin a version; budget a half-day for an API break per Unity LTS bump.

4. **AOT / IL2CPP.** Properties' source-gen path is the AOT-safe path (no reflection at runtime). This is actually a *win* over the reflection fallback, and roughly equivalent to today's hand-rolled emit.

5. **Convention attributes.** `[GraphPackage(Convention = AllFieldsIn)]` currently classifies *un-attributed* fields. The visitor needs the assembly's convention available at runtime — either bake it into a generated `GraphPackageInfo` static (small, easy) or read `[GraphPackage]` reflectively once at module init.

6. **Editor mirrors.** `GraphRegistryEmitter` also generates editor-side payload mirrors for the picker UI. Those are GraphFlow-specific and don't move; clarify whether the mirror needs `PortMeta[]` or can also lean on the bag.

## Risks

- **Two generators, one compilation** — see open question 2; if Properties' generator can't see attributes we emit, the migration story for *closed* user types gets awkward.
- **Performance on first port bind** — `IProperty.GetAttribute<T>()` is O(attrs); cache per-property in the binder if profiled hot.
- **Loss of static introspectability** — today you can grep generated catalogs and see every port. With lazy bag-driven derivation, the answer lives in compiled IL. Fine for runtime, slightly worse DX.

## Spike plan

Goal: prove the inversion on **one** representative payload type before committing.

1. Add `com.unity.properties` to the runtime asmdef. Confirm it builds in Editor + Player (IL2CPP).
2. Pick one event type (e.g. `OnPlay`) and one command (e.g. an existing `CommandBase<T>` payload). Manually annotate with `[GeneratePropertyBag]` and the assembly attribute.
3. Hand-write `GraphPortBinder`. Wire it into the runtime path that today calls into the generated registry block.
4. Keep the existing generator output untouched for the rest of the assembly — proves coexistence.
5. Confirm: M0 + CardSandbox tests still pass, port reads/writes still produce identical results, no allocations on hot paths (Profiler).
6. Decision gate: if the visitor approach is cleaner *and* no regressions, plan the generator delta (drop registry per-field block, keep catalog) as a follow-up phase.

If the spike succeeds, the migration is mostly subtraction from `GraphRegistryEmitter` plus adding one `[GeneratePropertyBag]` emit per discovered type. If it fails (coexistence, perf, AOT), we keep status quo and the per-field plumbing stays where it is.

## Non-goals

- Replacing the catalog itself with PropertyBag — Unity Properties has no kind/enum/factory concept; the catalog stays.
- Replacing serialization or data binding — those would be additional wins on top, not part of this slice.
- Migrating editor UI to use Properties' built-in inspectors — out of scope.
