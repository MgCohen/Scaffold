# Coding standards

Apply these rules when writing or reviewing code in the project.

## Comments

- **No comments on methods**, only on classes (and types).
- **Exception:** "todo" and "sample" comments are allowed anywhere (e.g. inline sample comments in a method body explaining placeholder or example behavior).

## One class per file

- **Do not put more than one class per file**, unless:
  - One of the classes is **private** (e.g. a private nested class in the same file), or
  - It is a **generic variation** (e.g. `Foo` and `Foo<T>` in the same file).

## Public API and dead code

- **Do not create public fields, properties or methods that are not used**, unless they are part of an API or interface (e.g. interface members, abstract/virtual members, public surface intended for callers).
- **During a refactor**, if a public field, property or method loses all uses and it is not part of an API or interface, **delete it** instead of leaving it unused.
- **Intended entry points are exempt.** Methods that define the designed behavioural contract of a class (e.g. `StartTurn()` on `Match`) should be kept even without current callers. Use judgement to distinguish designed API from accidentally public members: designed API describes *what a class does*; accidentally public members are implementation details that leaked out.

## Method order

- **Methods should be in order of usage.** If method A uses B, then B should appear after A. If method A uses B and C, then B should appear after A and C after B (order: A, B, C).
- **Exception:** If B is used by a method that already appears before A, then B stays after that method and before A (so B is not moved to immediately after A).
- **Internal (nested) classes** stay at the end of the containing class, no matter where they are used.

## Callbacks and subscriptions

- **When subscribing to callbacks and actions, avoid lambdas when possible.** If the callback has multiple parameters and you only need one (or some), use a method that matches the callback signature and ignore the unused parameter(s) in the method instead of wrapping in a lambda.

## Nested calls

- **Do not nest function calls.** Assign the result of each call to a named variable and pass that variable to the next call.
- **Do not nest object construction more than one level deep.** Passing `new Foo()` directly as an argument is fine; nesting a constructor inside that is not — assign the inner object to a variable first.
- **Exception:** When a parameter expects a callback (`Action`, `Func`, or delegate), passing a method reference directly is allowed.

```csharp
// ❌ Nested call
SetActivePlayer(GetNextPlayer(state));

// ✅ Named intermediate
var nextPlayer = GetNextPlayer(state);
SetActivePlayer(nextPlayer);

// ❌ Nested construction (two levels deep)
Execute(new SetActivePlayersMutator(new List<MatchPlayer> { player }));
// ✅ Named intermediate
var players = new List<MatchPlayer> { player };
Execute(new SetActivePlayersMutator(players));

// ✅ Single-level construction is fine
Execute(new EndRoundMutator());

// ✅ Callback exception — method reference passed as Action
var context = new PhaseContext(AdvancePhase);
```

## Method body syntax

- **Always use curly-bracket bodies for methods in a class.** Do not use expression-body (`=>`) syntax for class methods, even if the body is a single expression.
- **Use `=>` only for actual lambdas** (e.g. anonymous functions, delegates passed as arguments, or `Action`/`Func` assignments).

```csharp
// ❌ Expression-body method
private bool HasPlayers(PlayerPriorityState state) => state.PlayerOrder != null && state.PlayerOrder.Count > 0;

// ✅ Curly-bracket method
private bool HasPlayers(PlayerPriorityState state)
{
    return state.PlayerOrder != null && state.PlayerOrder.Count > 0;
}

// ✅ Lambda (correct use of =>)
_store.Subscribe<TurnState>((_, state) => OnTurnStateChanged(state));
```

## Line breaks

- **Avoid line breaks** unless you are using Fluent or Builder patterns (where multi-line chaining is idiomatic).
- **Constructors, method signatures, and similar declarations** stay on one line even when long (e.g. a constructor with many parameters). Do not split parameters or the signature across multiple lines.
