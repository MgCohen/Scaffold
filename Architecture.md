# Architecture

This document describes the high-level architecture of the **Scaffold** project. If you want to familiarize yourself with the codebase and how its foundational systems fit together, you are in the right place.

## Bird's Eye View

On the highest level, the Scaffold repository is a modular, layered framework for building robust Unity games. It separates generic foundational infrastructure (Dependency Injection, Events, MVVM UI, Networking) from domain-specific game code. 

The project strictly enforces architectural standards at compile time, treating code quality as a structural invariant. Things like "one class per file", "no nested calls", and "mandatory use of immutable records for state" are not just conventions but rules upheld by custom Roslyn analyzers.

This split guarantees that game logic (`GameModule`) can aggressively utilize UI or network systems without becoming tightly coupled to Unity's `MonoBehaviour` lifecycle, and that the infrastructure (`Infra`) can be iterated upon or swapped without breaking the core game domains.

## Code Map

The architecture consists of three main layers, progressively building upwards from generic utilities to specific bindings.

### `Assets/Scripts/Infra/` (Infrastructure Layer)

This directory contains the foundational packages that make up the "Scaffold" framework itself. They are layered strictly:
- **Layer 0 (Core)**: 
  - `Scaffold.Containers`: Wraps Dependency Injection (VContainer) to provide project-specific abstractions (`IContainerRegistry`, `IContainerResolver`).
  - `Scaffold.NetworkMessages`: Serializes and deserializes messages over Unity's Netcode.
- **Layer 1 (Services)**:
  - `Scaffold.Events`: A many-to-many event bus (`IEventBus`) used to broadcast immutable state records.
  - `Scaffold.Navigation`: Screen and route management based on shared schemas and types.
- **Layer 2 (UI & Wiring)**:
  - `Scaffold.MVVM`: A full Model-View-ViewModel UI framework for Unity. Allows `ViewModels` to subscribe to the event bus and output generic bindings.
- **Layer 3 (Extensions)**:
  - `Scaffold.MVVM.Extensions`: Unity-specific integrations, such as binding TextMeshPro fields to `MVVM` properties.

### `Scaffold.Analyzers/`

A set of custom Roslyn C# analyzers. Instead of relying purely on code reviews, this project enforces its conventions via compile-time errors. This is the boundary enforcer that ensures all architectural rules are permanently followed.

### `GameModule/`

The actual game logic and state protocols reside here. It treats the `Scaffold.*` assemblies as its infrastructure. It builds upon `Scaffold.Records` and `Scaffold.Events` to emit state changes, which the `MVVM` layer subsequently displays.

### Utility Assemblies

Foundational shared abstractions:
- `Scaffold.Records`: Centralizes the definition of immutable records syntax.
- `Scaffold.Types` & `Scaffold.Maps`: Shared collections and primitives (e.g. `Optional`).

## Architecture Invariants

The following invariants describe what is deliberately *absent* from the codebase, or how things are strictly mandated:

- **One Class Per File**: A file maps 1:1 to a type, avoiding hidden nested types unless strictly private.
- **No Direct Mutation of State**: Game state and event payloads are immutable `record`s. To alter state, you must use the `with` expression to derive a new instance (`state with { Value = state.Value + 1 }`).
- **No Expression-Body Methods**: Class methods strictly use curly-brackets `{ ... }`. `=>` is reserved exclusively for lambdas, getters, and operator overloads.
- **No Nested Function Calls**: The result of any call must be assigned to an explicitly named variable before being passed into the next function to preserve step-by-step readability.
- **Strict Namespace Layering**: The root namespace is always `Scaffold.`. The `Infra/` folder is ignored in namespaces; instead, the feature dictates the name (e.g., `Scaffold.MVVM`).
- **Absence of Tangled Lifecycles**: `MonoBehaviour` `Start`/`Update` are minimized. Service lifecycles are dictated by `Scaffold.Containers` (Singleton, Scoped, Transient) using constructor injection.

## Cross-Cutting Concerns

### Dependency Injection
Constructor injection is the primary way modules receive their dependencies. Components that must be created dynamically use `IContainerResolver`. The `VContainer` package provides the backbone but is deliberately abstracted away behind `Scaffold.Containers` to prevent hard-coupling.

### Event Broadcasting
Instead of traditional C# `event` coupling between modules, the project relies on `Scaffold.Events`. Any system can broadcast a `ContextEvent` (an immutable record payload). Other systems can subscribe anonymously, establishing a massive degree of decoupling between UI (`MVVM`), Input, and Network layers.

### Code Generation & Compile-Time Checks
The compilation pipeline is intercepted by `Scaffold.Analyzers` which parse the syntax tree. Any deviation from the architectural rules (like placing two public classes in one file, or omitting the `Scaffold.` namespace) results in compilation failure.
