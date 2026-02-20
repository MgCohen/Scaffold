# Scaffold — Project context for new AI chats

Use this file at the start of a chat so the AI has full project context. Summaries and links only; open linked files for full text.

---

## Quick summary

- **What Scaffold is:** Unity framework with a central **State Store** (Scaffold.States), **Infra** (DI/Containers, Events, MVVM, Navigation), and **Game**/sample modules.
- **Core pattern:** State lives in the Store; mutations go through Mutators; controllers hold entities and read/write only via Store.
- **Where to look:** Game logic → `Assets/Scripts/Game/` and `Assets/Samples/`; Infra → `Assets/Scripts/Infra/`; rules → `.cursor/rules/`; skills → `.cursor/skills/`; deeper context → `.cursor/context/`.
- **Coding standards are mandatory:** When writing, editing, or reviewing any code in this project, you **must** read [.cursor/rules/coding-standards.md](.cursor/rules/coding-standards.md) in full and follow every rule in that file. Do not rely only on the summary below.

---

## Project structure

```
Scaffold/
├── .cursor/
│   ├── context/          # Project and module context (this file, sampleturn-module, containers-refactor)
│   ├── plans/             # Completed and in-progress plans
│   ├── rules/             # Coding and infra guidelines
│   └── skills/            # Game-state and scaffold-infra skills
├── Assets/
│   ├── Scripts/
│   │   ├── Game/          # State, Turn, Stack, WIP
│   │   ├── Infra/         # Containers, Events, MVVM, Navigation
│   │   └── Utility/       # Record, Maps, Types
│   └── Samples/
│       ├── SampleTurn/    # Reference gameplay module (turn-based loop)
│       └── Container/     # Bootstrap sample
├── TestPackage/
└── GeneratorCustom/
```

**Key asmdefs:** `Scaffold.States` (Game/State), `Scaffold.Containers` (Infra/Containers), `Scaffold.Events`, `Scaffold.MVVM`, `Scaffold.Navigation`, `Scaffold.Records` (Utility).

---

## Important file paths

### State (Scaffold.States)

| Path | Purpose |
|------|--------|
| `Assets/Scripts/Game/State/Runtime/Store.cs` | Central store: `Get<TState>()`, `Execute(mutator)`, `Subscribe<TState>()` |
| `Assets/Scripts/Game/State/Runtime/State/State.cs` | Base type for state slices |
| `Assets/Scripts/Game/State/Runtime/Mutators/Mutator.cs` | Base `Mutator<TState>`; implement `Change(TState)` |
| `Assets/Scripts/Game/State/Runtime/Builders/Store/StoreBuilder.cs` | Build Store with slices (e.g. `BuildSlice(initialState)`) |

### Containers (post-refactor)

| Path | Purpose |
|------|--------|
| `Assets/Scripts/Infra/Containers/Runtime/Implementation/Boostrap.cs` | MonoBehaviour entry; `GetAdapter().Run(transform, Build)` |
| `Assets/Scripts/Infra/Containers/Runtime/Implementation/Container.cs` | Override `Build(IContainerRegistry, Transform)`; `BuildInternal` is internal trampoline for adapter |
| `Assets/Scripts/Infra/Containers/Runtime/Abstractions/IContainerRegistry.cs` | Registration API (replaces old IContainerBuilder) |
| `Assets/Scripts/Infra/Containers/Runtime/Internal/` | VContainer adapters (e.g. `VContainerAdapter.cs`, `VContainerScope.cs`); no `Runtime/Adapters/` folder |

### SampleTurn (reference gameplay module)

| Path | Purpose |
|------|--------|
| `Assets/Samples/SampleTurn/Match.cs` | Orchestrator; wires services and Store |
| `Assets/Samples/SampleTurn/MatchBuilder.cs` | Builds Store slices (TurnState, TurnOrderState, PriorityState) and Match |
| `Assets/Samples/SampleTurn/TurnState.cs`, `TurnOrderState.cs`, `PriorityState.cs` | State slices |
| `Assets/Samples/SampleTurn/TurnOrderService.cs`, `PriorityService.cs`, `TurnService.cs` | Services (Store only; mutate via mutators) |
| `Assets/Samples/SampleTurn/Mutators/` | SetCurrentPhaseMutator, EndRoundMutator, SetTurnOwnersMutator, SetActivePlayersMutator, RemoveActivePlayersMutator |

### Rules and skills

| Path | Purpose |
|------|--------|
| `.cursor/rules/coding-standards.md` | Comments, one class per file, API/dead code, method order, callbacks, nesting, method body, line breaks, records, small functions |
| `.cursor/rules/infra-folder-guidelines.md` | Infra layout (Abstractions/Implementation/Models), Container/Installer naming, installer visibility, .meta handling |
| `.cursor/skills/game-state-guidelines/SKILL.md` | State/Store/Mutator patterns; when to use |
| `.cursor/skills/scaffold-infra/SKILL.md` | Infra modules (Containers, Events, MVVM, Navigation); when to use |
| `.cursor/context/sampleturn-module.md` | SampleTurn architecture, data flow, state slices, services; patterns for new gameplay modules |
| `.cursor/context/containers-refactor.md` | Containers post-refactor (IContainerRegistry, Internal, no VContainer in public API) |

---

## Important rules and guidelines (summary)

Full text in the linked files above.

**Coding** ([.cursor/rules/coding-standards.md](.cursor/rules/coding-standards.md)) — **mandatory for all code**

- **You must read the full file and follow every rule** when creating or modifying any code. The list below is a reminder only.
- No comments on methods (only on classes/types); todo/sample comments allowed.
- One class per file (exceptions: private nested, generic variation).
- No unused public API; remove dead public members during refactors; intended entry points exempt.
- Methods in order of usage; avoid lambdas for subscriptions when a method will do; no nested calls or multi-level nested construction; curly-bracket method bodies (no `=>` for methods); avoid line breaks except Fluent/Builder; records use inline constructor form; small focused functions.

**State** ([.cursor/skills/game-state-guidelines/SKILL.md](.cursor/skills/game-state-guidelines/SKILL.md))

- State in Store only; controllers do not hold state; mutations via `Mutator<TState>` and `store.Execute(mutator)`; controllers hold entity references; “current” from Store, “all possible” from controller entities.

**Infra** ([.cursor/rules/infra-folder-guidelines.md](.cursor/rules/infra-folder-guidelines.md))

- Abstractions vs Implementation vs Models; Container/Installer naming (`[module]Container`, `[module]Installer`); concrete installers public; delete/move .meta with assets.

---

## Required context by task

**For any code change (all tasks):**

- Read [.cursor/rules/coding-standards.md](.cursor/rules/coding-standards.md) in full and apply every rule to the code you write or edit.

**When working on gameplay or State:**

- Read [.cursor/skills/game-state-guidelines/SKILL.md](.cursor/skills/game-state-guidelines/SKILL.md) and [.cursor/context/sampleturn-module.md](.cursor/context/sampleturn-module.md).
- Follow: State types extend `State`; Mutators extend `Mutator<TState>`; register slices in builder; services/orchestrators use `store.Get` / `store.Execute`; react via `store.Subscribe`, not by calling behaviour directly after mutate.

**When working on Infra (Containers, Events, MVVM, Navigation):**

- Read [.cursor/skills/scaffold-infra/SKILL.md](.cursor/skills/scaffold-infra/SKILL.md) and [.cursor/rules/infra-folder-guidelines.md](.cursor/rules/infra-folder-guidelines.md).
- For Containers: use `IContainerRegistry` (not IContainerBuilder); adapters are in `Runtime/Internal/`; see [.cursor/context/containers-refactor.md](.cursor/context/containers-refactor.md) for current architecture.

---

## How to use this file

- **For AI:** Consider `@.cursor/context/project-context.md` at the start of a new chat, or include it via Cursor rules so it is automatically considered.
- **For humans:** Single place to see structure, key paths, and which rules/skills/context docs to open for a given task.
