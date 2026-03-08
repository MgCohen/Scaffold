# Scaffold — AI Agent Instructions

This is the **primary context file** for all AI coding agents in this repository. Read it fully before making any changes. It takes precedence over any other instructions.

## Project Summary

Scaffold is a highly modularized Unity project (C#) built on MVVM architecture, enforcing strict separation of concerns via custom Roslyn analyzers and source generators. Each module is encapsulated in its own assembly (`.asmdef`/`.csproj`) with explicit dependencies.

## Tech Stack

- **Engine**: Unity
- **Language**: C#
- **Architecture**: MVVM (Model-View-ViewModel)
- **Code Enforcement**: Custom Roslyn Analyzers (`Analyzers/`)
- **Code Generation**: Custom Roslyn Source Generators (`Generators/`)
- **Dependency Injection**: Custom IoC Container (`Assets/Scripts/Infra/Containers/`)

## Directory Structure

```
Doc/                    # Documentation — every module must have a .md here
Plan/                   # All planning documents and ExecPlans
Assets/Scripts/
  Core/                 # Core application logic (MVVM base, domain)
  Infra/                # Containers, Events, Navigation, NetworkMessages
  Tools/                # AutoPacker, Maps, Records, Types
  Presentation/         # UI and visual representation layer
Analyzers/              # Custom Roslyn analyzers
Generators/             # Source generators (AutoPacker, ObservableNestedProperties)
Packages/               # Unity packages and third-party dependencies
```

For full detail on modules and relationships, see [Architecture.md](Architecture.md).

## Code Rules (Non-Negotiable)

1. **Separation of Concerns**: Never couple core logic with Unity-specific presentation.
2. **Modular Integrity**: Declare all dependencies explicitly via `.asmdef`/`.csproj`. Never bypass architectural boundaries.
3. **No MonoBehaviours in Core**: MonoBehaviours are restricted to bootstrap and presentation layers only.
4. **Mandatory Tests**: Every module must have tests.
5. **Documentation**: Every module needs a `.md` file or folder under `Doc/`.
6. **Plans go in `Plan/`**: All planning documents belong there.
7. **New source generators go in `Generators/`**.
8. **Analyzer compliance**: Fix all Roslyn analyzer warnings/errors before committing.

## How to Build

- **Application**: Standard Unity Editor pipeline (`File > Build Settings`) or Unity CLI batch mode.
- **Analyzers & Generators**: These are standard .NET projects. After modifying them, run `dotnet build` in their directory to regenerate the DLLs that Unity picks up during compilation.

## How to Test

- **Unity Test Runner**: `Window > General > Test Runner` — run both `EditMode` and `PlayMode` tests.
- **Headless**: Unity CLI with `-runTests` flag.

## Workflows

### Creating a New Module
Use the `/create-module` workflow (`.agents/workflows/create-module.md`). Do **not** manually copy-paste folder structures.

### Creating a Custom Analyzer Rule
Use the `/create-custom-analyzer` workflow (`.agents/workflows/create-custom-analyzer.md`).

## Where to Put Plans

All planning documents are **ExecPlans** and live in `Plan/` as `.md` files. Never put plans anywhere else.

The full ExecPlan specification is in [PLANS.md](PLANS.md) — read it before authoring or executing any plan. Key rules:

- One file per feature/task, named descriptively (e.g., `Plan/add-navigation-system.md`).
- Every ExecPlan must be **self-contained** — a novice must be able to implement the feature end-to-end using only that file.
- ExecPlans are **living documents** — update them continuously as work proceeds.
- Required sections: `Progress`, `Surprises & Discoveries`, `Decision Log`, `Outcomes & Retrospective`.
- Do not prompt the user for next steps; proceed autonomously milestone to milestone.
- Commit frequently.

## Where to Put Documentation

All module documentation lives in `Doc/`. Every module **must** have a corresponding `.md` file or subfolder there. No exceptions.

Naming convention mirrors the module path:
- A module at `Assets/Scripts/Infra/Events/` → document at `Doc/Infra/Events.md` (or `Doc/Infra/Events/`)
- A new tool at `Assets/Scripts/Tools/Foo/` → document at `Doc/Tools/Foo.md`

Documentation must cover: purpose, public API/interface, usage examples, and any non-obvious design decisions. Keep it up to date as the module evolves.

## Agent Behavior Guidelines

- Read `Architecture.md` for module details before touching any code.
- Read `PLANS.md` before authoring or executing any ExecPlan.
- Always fix analyzer warnings before committing.
- Prefer editing existing files over creating new ones.
- Keep changes minimal and focused — no speculative abstractions or premature generalization.
- Use existing modules as reference when unsure about patterns.
