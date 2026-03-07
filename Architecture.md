# Architecture

This document describes the high-level architecture of the Scaffold project. It serves as a bird's-eye view for anyone looking to understand the project structure and contribute effectively.

## Project Summary

Scaffold is a highly modularized Unity project designed with clear boundaries. It enforces strict separation of concerns, dividing functionality into core logic, infrastructure components, and development tools.

This approach ensures that the project remains scalable, testable, and maintainable. Each module is encapsulated within its own assembly (`.asmdef` or `.csproj`), keeping dependencies explicit and minimizing tight coupling.

## Tech Stack

The project relies on the following core technologies and patterns:
- **Engine**: Unity
- **Language**: C#
- **Architecture**: MVVM (Model-View-ViewModel)
- **Code Analysis & Generation**: Custom Roslyn Analyzers and Source Generators
- **Dependency Injection**: Custom IoC Container

## Bird's Eye View & File Listing

The repository contains the following main directories:

- `Doc/`: Documentation files for all project modules. Every module must have a `.md` document/folder here containing its logic.
- `Plan/`: Contains all planning documents and files.
- `Assets/`: The main Unity directory.
  - `Scripts/`: Contains all the application source code divided into functional layers.
    - `Core/`: Core application logic.
    - `Infra/`: Infrastructure systems and framework-level tools.
    - `Presentation/`: User interface and visual representation layer.
    - `Tools/`: Helper utilities and structural tools.
- `Analyzers/`: Contains custom Roslyn analyzers that enforce codestyle and architectural rules.
- `Generators/`: Source generators (e.g., AutoPacker and ObservableNestedPropertiesGenerator) that output boilerplate code during compilation.
- `Packages/`: Project dependencies and third-party modules.

## Modules

The logic is split into several interconnected but distinct submodules located inside `Assets/Scripts`:

### Core
- **MVVM**: Implements the Model-View-ViewModel architectural pattern. This module provides the foundation for data-binding and separating the UI logic from the presentation layer.

### Infra
- **Containers**: Dependency injection and inversion of control (IoC) resolution mechanisms to manage service lifecycles.
- **Events**: A decoupled event-bus or messaging system used for cross-module communication without direct references.
- **Navigation**: Handling in-app routing, view transitions, and screen management.
- **NetworkMessages**: Structures and subsystems dedicated to handling data transfer or network communication payloads.

### Tools
- **Maps**: Utilities mapped to data collections or routing maps.
- **Records**: Tools relating to immutable data structures or state records.
- **Types**: Custom type wrappers, extensions, and type-safety enforcers. 

## Basic Rules and Code Standards

The project follows a strict set of rules to maintain code quality:
1. **Separation of Concerns**: Never tightly couple core logic with Unity-specific presentation.
2. **Modular Integrity**: Always declare dependencies explicitly. One module should not bypass the intended architectural boundaries to access another.
3. **Automated Enforcement**: All rules and standards are defined through a custom Roslyn analyzer located in the `Analyzers/` folder.
4. **Avoid MonoBehaviours for Core Logic**: MonoBehaviours must be avoided when writing core business logic.
5. **Restrict MonoBehaviours**: Try, as much as possible, to leave MonoBehaviours only to bootstrap and presentation logic.
6. **Mandatory Testing**: We must have tests in all modules.
7. **Documentation**: All modules need a `.md` document or folder within the `Doc/` directory containing their logic documentation.
8. **Plans**: All plan files must go into the `Plan/` directory.
9. **Source Generators Location**: When creating new source generators, they should be placed in `Generators/`.

If the custom analyzer reports a warning or error, you must fix it to comply with the project standards before committing code. 

## How to Create More Code

To maintain structural consistency, you should not manually copy-paste folders to create new pieces of logic.

### Creating a New Module
When you need to create a new module, you must use the provided workflow. The `/create-module` workflow automatically scaffolds a new module following the project's structure guidelines, ensuring the correct `.asmdef`/`.csproj` boundaries and folder structure.

### Creating a Custom Analyzer Rule
If you need to define or enforce a new architectural rule, you should use the custom analyzer workflow. Run the `/create-custom-analyzer` workflow, which guides you through creating a new custom Roslyn analyzer linter rule that runs securely across the codebase.
