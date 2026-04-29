# com.scaffold.entities.states

Bridge package: **state-backed** entity instances. Variable reads match **`BaseEntityInstance` / `EntityInstance`**, but writes go through **`Scaffold.States.Store`** (`Execute` with payload records) so **`SaveSnapshot` / `LoadSnapshot`** include modifier stacks and base overrides.

## Contents

- **`EntityVariableState`** — canonical slice (`BaseValues`, ordered **`ModifierStacks`**, cached **`EffectiveValues`**).
- Payloads: **`AddModifierPayload`**, **`RemoveModifierPayload`**, **`SetBaseValuePayload`**, **`AddEntityVariablePayload`**.
- Mutators — registered once per **`Store`** (via **`EntityBridgeContext`**); **`EntityStateFactory.Create`** binds **`InstanceId`** → **`IEntityDefinition`**.
- **`StoreVariableStorage`** — **`IEntityVariableStorage`**; one store subscription for **`EntityVariableState`** per entity with local callback fan-out.
- **`StateEntity<TDefinition>`** — read-only surface (does **not** implement **`IMutableEntity<TDefinition>`**).

## Usage

```csharp
var store = new StoreBuilder().Build();
var id = new InstanceId(1);
var entity = EntityStateFactory.Create(definition, store, id);
store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
var v = entity.GetVariable<float>(hp);
```

Authoritative module pointer: [`Docs/Core/EntitiesStates.md`](../../../Docs/Core/EntitiesStates.md).
