---
name: game-state-guidelines
description: Guidelines for integrating gameplay modules with the Scaffold.States Store. Use when building or refactoring gameplay modules that must store state in the Store, when controllers should read/write state via Store and Mutators, or when the user asks for State/Store/Mutator patterns.
---

# Game State Guidelines (Scaffold.States)

Apply these rules when implementing or refactoring any gameplay module that uses [Assets/Scripts/Game/State/](Assets/Scripts/Game/State/).

## 1. State lives in the Store

- Any state is stored as a **State** (type extending `Scaffold.States.State`) in the [Store](Assets/Scripts/Game/State/Runtime/Store.cs).
- State types are registered as slices (e.g. via `StoreBuilder().BuildSlice(initialState).Build()`).
- Use `store.Get<TState>()` or `store.Get<TState>(reference)` to read; never hold a reference to state on a controller.

## 2. Controllers do not hold state

- Controllers must **not** keep a reference to state.
- If a controller needs to check state, it **requests** it from the Store with `store.Get<TState>()`.
- Controllers still hold references to their **entities** (e.g. list of phases, list of players).

## 3. Mutations go through Mutators

- If a controller needs to **modify** state, it does so via a [Mutator&lt;TState&gt;](Assets/Scripts/Game/State/Runtime/Mutators/Mutator.cs) created for that purpose.
- Execute mutators with `store.Execute(mutator)` or `store.Execute(reference, mutator)`.
- Mutators are classes extending `Mutator<TState>` and implementing `TState Change(TState state)` (pure function: take state, return new state). Use immutable state (e.g. record with `init` properties) and `with` expressions.

## 4. Controllers keep references to entities

- A controller holds references to its **entities** (e.g. Match holds a list of Phase, list of MatchPlayer).
- "What are the possible X?" → defined by the controller’s entity list.
- "What is the current X?" → read from the Store (e.g. `store.Get<TurnState>().CurrentPhase`).

## Example (SampleTurn)

- **Match** holds `IReadOnlyList<Phase>` and `IReadOnlyList<MatchPlayer>` (entities). It receives a **Store** (e.g. from MatchBuilder) and does not hold TurnState.
- **Current phase** → `store.Get<TurnState>().CurrentPhase`.
- **Possible phases** → `match.Phases`.
- **Changing current phase** → `store.Execute(new SetCurrentPhaseMutator(phase))`.
- **TurnState** extends `State`; properties use `{ get; init; }`. Initial state is built by the builder and registered as a slice; the builder passes the Store into the controller.

## Summary

| Need | Action |
|------|--------|
| Read state | `store.Get<TState>()` |
| Change state | Create a `Mutator<TState>`, then `store.Execute(mutator)` |
| Know "current" value | Get from Store |
| Know "all possible" values | Use controller’s entity list |
