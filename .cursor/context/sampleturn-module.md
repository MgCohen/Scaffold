# SampleTurn Module — Context

## What this module is

`SampleTurn` is a sample implementation of a turn-based game loop built on top of the `Scaffold.States` store. It demonstrates the correct patterns for any gameplay module in this project.

---

## File structure

```
Assets/Samples/SampleTurn/
├── Match.cs                          # Orchestrator — creates and wires services
├── MatchBuilder.cs                   # Constructs Match + initialises Store slices
├── MatchPlayer.cs                    # Player entity
├── Phase.cs                          # Abstract phase entity + IPhaseContext
├── PhaseFactory.cs                   # Creates Phase entities with auto-incremented IDs
├── PriorityState.cs                  # State: ActivePlayers
├── PriorityService.cs                # Syncs active players from turn owners; IsActive
├── TurnOrderState.cs                 # State: PlayerOrder, TurnOwners
├── TurnOrderService.cs               # AdvanceToNext, MoveToFirst
├── TurnService.cs                    # Manages Turn entity lifecycle
├── TurnState.cs                      # State: CurrentRoundIndex, CurrentPhase
├── Turn.cs                           # Entity: one turn; created fresh each time
├── Phases/
│   └── DiscardPhase.cs               # Example Phase implementation
└── Mutators/
    ├── SetCurrentPhaseMutator.cs     # Sets TurnState.CurrentPhase
    ├── EndRoundMutator.cs            # Increments TurnState.CurrentRoundIndex
    ├── SetTurnOwnersMutator.cs       # Replaces TurnOrderState.TurnOwners
    ├── SetActivePlayersMutator.cs    # Replaces PriorityState.ActivePlayers
    └── RemoveActivePlayersMutator.cs # Removes players from PriorityState.ActivePlayers
```

---

## Architecture

```
MatchBuilder
  └── builds Store (TurnState + TurnOrderState + PriorityState slices)
  └── builds Match(players, phases, store)

Match (orchestrator)
  ├── TurnOrderService  — turn order: AdvanceToNext, MoveToFirst
  ├── PriorityService   — who has priority: SetNextActivePlayers, IsActive
  └── TurnService      — creates and runs Turn entities
       └── Turn (entity, one per turn)
            ├── subscribes to TurnState changes
            └── on change → EnterPhase → phase.OnEnter(activePlayers from PriorityState, context)
```

### Data flow (runtime)

1. `match.StartTurn()` → `TurnService.StartTurn()` → `new Turn(...)` → `turn.RunCurrentPhase()`
2. `RunCurrentPhase()` executes `SetCurrentPhaseMutator` → Store updates `TurnState`
3. Store notifies `Turn.OnTurnStateChanged` → `EnterPhase` → `phase.OnEnter(activePlayers, context)` (activePlayers from `store.Get<PriorityState>().ActivePlayers`)
4. Phase signals completion via `context.Complete()` → `Turn.AdvancePhase()`
5. If more phases remain: advance index → `RunCurrentPhase()` → repeat from step 2
6. If all phases done: `_onTurnEnded` → `Match.OnTurnEnded()` → `TurnOrderService.AdvanceToNext()` + `PriorityService.SetNextActivePlayers()` + `TurnService.StartTurn()`

---

## State slices

### `TurnState`
| Property | Type | Description |
|----------|------|-------------|
| `CurrentRoundIndex` | `int` | Round counter, incremented by `EndRoundMutator` |
| `CurrentPhase` | `Phase` | The phase currently being executed |

### `TurnOrderState`
| Property | Type | Description |
|----------|------|-------------|
| `PlayerOrder` | `IReadOnlyList<MatchPlayer>` | The ordered list of players (turn order) |
| `TurnOwners` | `IReadOnlyList<MatchPlayer>` | Who currently owns the turn (can be more than one) |

### `PriorityState`
| Property | Type | Description |
|----------|------|-------------|
| `ActivePlayers` | `IReadOnlyList<MatchPlayer>` | Who currently has priority to act (can be more than one) |

---

## Services

### `TurnOrderService`
Holds only `Store`. Reads `TurnOrderState`; mutates via `SetTurnOwnersMutator`.

| Method | Description |
|--------|-------------|
| `AdvanceToNext()` | Advances TurnOwners to next in PlayerOrder (wraps around) |
| `MoveToFirst()` | Sets TurnOwners to PlayerOrder[0] |

### `PriorityService`
Holds only `Store`. Reads `TurnOrderState` and `PriorityState`; mutates via `SetActivePlayersMutator`.

| Method | Description |
|--------|-------------|
| `SetNextActivePlayers()` | Copies TurnOrderState.TurnOwners to PriorityState.ActivePlayers |
| `IsActive(MatchPlayer)` | Returns true if player is in ActivePlayers |

### `TurnService`
Holds phases list, `Store`, and `onTurnEnded` callback. Creates a fresh `Turn` each time.

| Method | Description |
|--------|-------------|
| `StartTurn()` | Creates `new Turn(phases, store, onTurnEnded)`, calls `RunCurrentPhase()` |

---

## Key design rules (as applied here)

- **State lives in the Store.** No controller holds state directly. All reads go through `store.Get<T>()`.
- **Mutations go through Mutators.** All writes go through `store.Execute(mutator)`.
- **Controllers hold entity references.** `TurnService` holds phases; `Match` passes players/phases at construction time.
- **Turn is an entity, not a service.** Created fresh per turn; owns only the phase index cursor.
- **Match is a pure orchestrator.** Creates services, wires the `OnTurnEnded` callback — no game logic.
- **Phase.OnEnter receives active players.** `IReadOnlyList<MatchPlayer>` — multiple players can be active simultaneously.

---

## Patterns to follow when adding new gameplay modules

1. Create a `XState : State` record for any observable state; register as a Store slice in the builder.
2. Create a `XMutator : Mutator<XState>` for each distinct state change.
3. Create a `XService` that holds only `Store`; reads and mutates via the Store.
4. If the module produces entities (like phases, cards), keep them as lists in the controller/service — not in the Store.
5. Wire services together in the orchestrator (e.g. `Match`), not in the services themselves.
6. Subscribe to state changes (`store.Subscribe<TState>`) to react to transitions; do not call behaviour directly after mutation.
