# Infra module folder and interface/implementation guidelines

Apply these rules when organizing or adding code under `Assets/Scripts/Infra/` (Containers, Events, Navigation, MVVM, and any new Infra modules).

## Folder layout

- **Runtime/Abstractions/** – Interfaces (`I*`) and abstract base/contract types that define the module’s public shape.
- **Runtime/Implementation/** – Concrete classes that implement those interfaces or extend those bases.
- **Runtime/Models/** – DTOs, enums, and data-only types (no behavior). Optional; use when the module has clear data types.
- **Runtime/Events/**, **Runtime/Enums/**, **Runtime/Utility/** – Use as needed. Keep **Adapters/**, **Container/** etc. where they already exist as feature folders.

Namespaces stay unchanged (e.g. `Scaffold.Events`); only folder paths follow this layout.

## Base types and contract types (Abstractions vs Implementation vs Models)

- **Abstractions** is for both interfaces and **abstract base / contract types** that other types extend or implement. Examples: `IEventBus`, and an **abstract** `ContextEvent` that all event payloads inherit from. Do not put these in Implementation.
- **Implementation** is only for **concrete** types that implement an interface or extend an abstract type. If a type is the root of a hierarchy (e.g. an abstract base class or record), place it in **Abstractions** (or **Models** if it is purely a data shape with no behavioral contract).
- **Models** is for data/shape types: DTOs, records used as payloads, enums. An empty or data-only base type (e.g. a record that only exists as a constraint base) can live in **Abstractions** (as a contract) or **Models** (as a data shape); prefer **Abstractions** when the type is the central contract of the module’s API.

Summary: abstract base types and contract types belong in **Abstractions** (or **Models** when they are purely data). Only concrete implementations go in **Implementation**.
