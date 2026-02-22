---
trigger: always_on
---

# Coding standards

**Mandatory for all code.** Anyone (including AI) writing, editing, or reviewing code in this project **must** read this entire file and follow every rule below. Do not rely on summaries in project-context or elsewhere; when creating or modifying any code, open this file and apply the full rules. No exceptions.

## Comments

- **No comments on methods**, only on classes (and types).
- **Exception:** "todo" and "sample" comments are allowed anywhere (e.g. inline sample comments in a method body explaining placeholder or example behavior).

## One class per file

- **Do not put more than one class per file**, unless:
  - One of the classes is **private** (e.g. a private nested class in the same file), or
  - It is a **generic variation** (e.g. `Foo` and `Foo<T>` in the same file), or
  - One of the types is an **event, record, or small data class that is created only by that file’s main class** — it may stay in the same file as long as it is purely a data type (even if used as a parameter or argument elsewhere).

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
- **Exceptions:** You are permitted to use the expression-body (`=>`) syntax for **get-only properties** and **operator overloads**.

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

// ✅ Get-only property (correct use of =>)
public Type SerializableType => typeof(SourceSample);

// ✅ Operator overload (correct use of =>)
public static implicit operator TypeReference(Type type) => new(type);
```

## Line breaks

- **Avoid line breaks** unless you are using Fluent or Builder patterns (where multi-line chaining is idiomatic).
- **Constructors, method signatures, and similar declarations** stay on one line even when long (e.g. a constructor with many parameters). Do not split parameters or the signature across multiple lines.
- **No line breaks inside methods.** Keep each statement or expression on one line. Do not split a single statement (e.g. an assignment, method call, or object construction) across multiple lines.

## Immutable records — inline constructor

When implementing immutable records, use the **inline (primary) constructor** form instead of a separate body with properties.

- **Reference the assembly**: Any project or asmdef that defines or uses records must reference the **Scaffold.Records** assembly so the record syntax is available.

### Preferred form

```csharp
// ✅ GOOD — inline constructor
public record Sample(int Value);
```

### Avoid

```csharp
// ❌ BAD — mutable-style body with separate property
public record Sample
{
    public int Value { get; init; }
}
```

Use the inline form for simple immutable data; it keeps the type declaration minimal and makes immutability obvious.

## Immutable records — use `with` to derive new instances

When you need a modified copy of an immutable record, use the `with` expression instead of constructing a new instance manually.

```csharp
public record MyState(int Value);

// ❌ BAD — manually constructing a new record
private MyState Increment(MyState state)
{
    return new MyState(state.Value + 1);
}

// ✅ GOOD — using with to derive a new instance
private MyState Increment(MyState state)
{
    return state with { Value = state.Value + 1 };
}
```

## Small, focused functions

- **One job per method**: Each method should do one thing and be nameable in a short phrase (e.g. "compute player priority", "advance to next phase").
- **Single level of abstraction**: Inside a method, don't mix high-level steps with low-level details. Either call other methods for the steps or inline the details, not both.
- **Short enough to grasp**: If a method is long or has nested conditionals/loops, extract steps into well-named methods so the main method reads like a short list of steps.
- **Extract instead of comment blocks**: If you're tempted to add a comment like "// Step 1: validate input", extract that into a method whose name is that step (e.g. `ValidateInput()`).

**Triggers for refactor:** If a method is **above 8 lines**, refactor it by extracting steps into well-named methods so it stays small and focused. If a method has a **return in the middle** (early return before the end of the body), refactor by extracting the early-exit path or the post-return logic into a separate method.

### Example

```csharp
// ❌ One long method doing several things
void ProcessTurn()
{
    if (currentPlayer == null) return;
    var priority = 0;
    foreach (var p in players) { priority = Mathf.Max(priority, p.Priority); }
    currentPlayer.Priority = priority + 1;
    if (currentPhase.CanAdvance) { /* 10 more lines */ }
}

// ✅ Focused methods, one level of abstraction
void ProcessTurn()
{
    if (!TryGetCurrentPlayer(out var player)) return;
    UpdatePlayerPriority(player);
    TryAdvancePhase();
}
```

## Naming conventions

- **Interfaces**: Start with `I` (e.g., `IMutator`).
- **Private fields**: Use `camelCase` (e.g., `currentPlayer`). Do not use `_` or `m_` prefixes.
- **Public fields and Properties**: Use `PascalCase` (e.g., `CurrentPlayer`).
- **Method names**: Use `PascalCase` verbs (e.g., `UpdateState`, `GetNextPlayer`).

## Asynchronous code

- **Awaitable vs Async/Await**: Give preference to Unity's `Awaitable` when writing asynchronous code. When `Awaitable` is not applicable, use standard `async/await`.

## Serialization

- **Inspector exposure**: Give preference to `[SerializeField]` on private fields over making fields public just for the Unity Inspector. 

## Dependency injection and resolution

- **Constructor injection**: Favor constructor injection whenever possible.
- **On-demand resolution**: When constructor injection is not possible, or when injecting multiple child classes, use an `IContainerResolver` (from `Scaffold.Containers`) and resolve or inject the class on demand.

## Callbacks and Events

Choose the appropriate event-driven approach based on the scope:
- **Direct 1-to-1**: Use direct callbacks (e.g., passing an `Action` or delegate to a specific object).
- **1-to-many**: Use standard C# `Action` or events.
- **Many-to-many or Anonymous**: Use the **Scaffold Events system** when either the caller or the sender is anonymous, or when broadcasting to many disconnected systems.
