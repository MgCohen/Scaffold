# Hearthstone Clone – Research & Thinking

## Goal
Map a Hearthstone card game system (no UI/presentation) onto the Scaffold framework.

---

## Scaffold Framework – Key Findings

### How the Store Works
- `Store` holds multiple `Slice` objects, each pairing an `IReference` + a `State` type.
- Lookup is two-dimensional: `Map<IReference, Type, Slice>` — so one `PlayerRef` can have many state types registered under it (`HeroState`, `ManaState`, `HandState`, etc.).
- Mutations go through `Mutator<TState>` which uses C# `with {}` expressions — all state is immutable records.
- Subscribers use `Store.Subscribe<TState>(IReference, callback)` — reactive, typed.

### How Commands & Effects Work
- `Command` = one async operation, one `store.Execute(mutator)` call. Fully atomic.
- `Effect` = orchestrates multiple commands via `ExecuteCommand()`. Handles validation, sequencing.
- `IGameEvents.SubscribeTo<TCommand>(callback)` fires **before and after** every command execution — this is the natural seam for reactive triggers (secrets, deathrattles, inspire).
- `EffectDirector.Execute(effect)` is the entry point — effects are queued, not immediate.
- The pipeline: `EffectDirector → EffectQueue → EffectExecutor → EffectRunner → effect.Execute() → ExecuteCommand() → pre-events → command.Execute() → post-events`.

### How the Turn System Works
- `ITurnHandler` manages `TurnState`: `CurrentTurn`, `ActivePlayer`, `PriorityPlayer`, `CurrentPhase`, `CurrentStep`.
- Phase and step are `IReference` values — custom phase records can be defined per game.
- `TurnHandler.Temporary_PassTurn()` increments the turn counter via a `ChangeTurn` mutator.

### How the Stack/Priority Works
- `PlayService` composes `IStackHandler`, `IPriorityHandler`, `IActionHandler`, `ITurnHandler`.
- Flow: `OpenWindow()` → `Play(stackable)` → `Pass()` (priority passes) → `CloseWindow()` → `ResolveStack()`.
- Hearthstone is simpler than MTG — no true interruptible stack. Priority auto-passes after each action.
- Secrets are the exception: they check via `IGameEvents` hooks, not by holding priority.

### Two Event Systems
- `IGameEvents` (Effects layer) — command-level hooks, used for **game triggers** (reactive abilities).
- `IEventBus` (Infra layer) — domain `ContextEvent` records, used for **notifications** (presentation, logging, tests).

---

## Hearthstone Rules Analysis

### What needs modelling
1. **Two players** with heroes (30 HP + armor), hero powers, weapons
2. **Mana** — 0→10 crystals, gain 1/turn, refill each turn
3. **Deck** (30 cards), **Hand** (max 10), **Board** (max 7 minions)
4. **Card types**: Minion, Spell, Weapon
5. **Keywords**: Taunt, Divine Shield, Charge, Rush, Windfury, Stealth, Lifesteal, Poisonous, Reborn, Inspire
6. **Abilities**: Battlecry (on play), Deathrattle (on death), Secret (conditional reaction)
7. **Combat**: attack once/turn, summoning sickness, taunt forces targeting
8. **Death**: batch-processed after each damage sequence, deathrattles fire in board order
9. **Turn flow**: Start (draw+mana) → Main (play/attack) → End (triggers+pass)
10. **Win condition**: enemy hero reaches 0 HP

### Key design tensions resolved
- **Where does each minion live?** As its own `MinionRef → MinionState` slice in the Store — not embedded inside `BoardState`. This allows per-minion subscriptions and clean state isolation. `BoardState` just holds an ordered list of `MinionRef`s.
- **How are card abilities extensible?** `IBattlecry`, `IDeathrattle`, `ISecret` interfaces, resolved by string Id via `IAbilityRegistry`. New cards = new definition + new ability class. Zero changes to core systems.
- **How do triggers work?** `IGameEvents.SubscribeTo<TCommand>` lets a `TriggerController` react to any command (e.g., a Secret that fires when a minion is summoned subscribes to `SummonMinionCommand`).
- **How are simultaneous deaths handled?** `DeathCheckEffect` runs after every damage sequence, collects all minions at ≤0 HP, processes them in board-order. This matches Hearthstone's batch death processing.
- **Card data vs card state?** `CardDefinition` is pure static data (not in Store). `CardState` in the Store tracks runtime state (current cost after discounts, owner). Definitions are loaded from JSON/ScriptableObjects at startup.

---

## Mapping Summary

| Hearthstone Concept | Scaffold Primitive |
|---|---|
| Game session | `GameRef → GameState` in Store |
| Player | `PlayerRef → HeroState, ManaState, HandState, DeckState, BoardState, WeaponState` |
| Minion on board | `MinionRef → MinionState` |
| Card instance | `CardRef → CardState` |
| Deal damage | `DealHeroDamageCommand` / `DealMinionDamageCommand` |
| Play a card | `PlayMinionEffect` / `PlaySpellEffect` / `PlayWeaponEffect` |
| Combat | `AttackEffect → DealDamageEffect → DeathCheckEffect` |
| Turn management | `ITurnHandler` + `StartTurnEffect` / `EndTurnEffect` |
| Battlecry/Deathrattle/Secret | `IGameEvents.SubscribeTo<TCommand>` in `TriggerController` |
| Notification to UI/tests | `IEventBus` + `ContextEvent` records |
| Stack resolution | `PlayService` with auto-priority-pass |
