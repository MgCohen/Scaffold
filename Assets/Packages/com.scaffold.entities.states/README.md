# com.scaffold.entities.states

Bridge package: **state-backed** entity variable data lives in **`Scaffold.States.Store`** as canonical **`EntityVariableState`** (base overrides and modifier stacks). A derived **`StateEntity<TDefinition>`** aggregate record (readable via **`Store.Get`**) holds resolved effective values. Writes use **`Store.Execute`** with payload records; **`SaveSnapshot` / `LoadSnapshot`** persist only canonical slices — aggregates rebuild from authored state after load.

## Contents

- **`EntityVariableState`** — canonical slice (`BaseValues`, ordered **`ModifierStacks`**). Instance methods **`WithModifier`**, **`WithoutModifier`**, **`WithBaseValue`**, **`WithVariable`**, and **`ResolveEffectiveValues`** return new records.
- **`StateEntity<TDefinition>`** — **`AggregateState`** record with **`Id`**, **`Definition`**, copies of base/stacks, and **`EffectiveValues`**; **`GetVariable<T>`** / **`TryGetVariable<T>`** match **`BaseEntityInstance`** read semantics. Does **not** implement **`IMutableEntity<TDefinition>`**.
- **`EntityStateProvider<TDefinition>`** — internal aggregate provider; wired by **`EntityStateFactory`**.
- Payloads: **`AddModifierPayload`**, **`RemoveModifierPayload`**, **`SetBaseValuePayload`**, **`AddEntityVariablePayload`**.
- **`EntityBridgeContext.RegisterMutators(Store)`** — call **once** per store before **`EntityStateFactory.Create`** registers slices.
- **`EntityStateFactory.Create`** — registers empty **`EntityVariableState`**, attaches the aggregate provider, returns the initial **`StateEntity<TDefinition>`** snapshot. Re-fetch with **`store.Get<StateEntity<TDefinition>>(id)`** or subscribe to **`StateEntity<TDefinition>`** events after further **`Execute`** calls.

## Usage

```csharp
var store = new StoreBuilder().Build();
EntityBridgeContext.RegisterMutators(store);
var id = new InstanceId(1);
var entity = EntityStateFactory.Create(definition, store, id);
store.Execute(id, new AddModifierPayload(id, hp, new FloatAddModifier(5f), ModifierId.New()));
var current = store.Get<StateEntity<EntityDefinition>>(id);
var v = current.GetVariable<float>(hp);
```

Authoritative module pointer: [`Docs/Core/EntitiesStates.md`](../../../Docs/Core/EntitiesStates.md).
