# Module Structure and Folder Guidelines

Apply these rules when creating or organizing any module in the project.

## New Module Creation

Whenever a new module is created, you must:
1. **Update Analysis**: Update the `Docs/Plans/infra-module-analysis.md` file to reflect the new module.
2. **Prevent Circular Dependencies**: Make sure the new module does not create circular dependencies.
3. **Prevent Unrelated Dependencies**: The new module must not create new dependencies that don't relate to itself. For example, if creating module A requires adding a new dependency between module B and C, flag it and ask for help before proceeding.

## Module Folder Splitting

A module is to be split into the following top-level folders:
1. **Runtime** -> All code and logic.
2. **Editor** -> (Optional) Any Editor-only scripts and tools.
3. **Container** -> All DI-related code, Installers, Containers, and anything that depends on Dependency Injection.
4. **Tests** -> Unit Testing.
5. **Samples** -> Any sample script, scene, or asset created for the sake of testing.
6. **Assets** -> (Optional) Non-script resources that belong to the module (prefabs, ScriptableObjects, data files, third-party DLLs / source generators). Do **not** use Unity's reserved `Resources` folder name.

## Assets Folder

The **Assets** folder holds any non-code files that a module needs at design-time or runtime but that are not C# source files. Typical contents include:

- **Data/** – ScriptableObject instances, configuration `.asset` files.
- **Prefabs/** – Prefab templates and variants owned by the module.
- **Generators/** – Source-generator DLLs and their dependency manifests.

The `Assets` folder does **not** have an assembly definition — it contains no compilable code.

## Assemblies

All folders should have their own assembly definition (`.asmdef`):
- **Runtime** -> `Scaffold.[Name of module]`
- **Editor** -> `Scaffold.[Name of module].Editor`
- **Container** -> `Scaffold.[Name of module].Container`
- **Tests** -> `Scaffold.[Name of module].Tests`
- **Samples** -> `Scaffold.[Name of module].Samples`

## Namespaces

The namespace inside each folder should follow the assembly name, with potential nested names for specific implementations.
For example: `Scaffold.Container.Adapter.VContainer`

## Tests

All modules **must** have tests. We use the test framework from Unity: `com.unity.test-framework`.
[Test Framework Documentation](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/test-framework-introduction.html)

## Runtime Folder Internal Structure

Inside a module's **Runtime** folder, the layout follows these rules:

- **Contracts/** – Interfaces (`I*`), abstract base classes, and contract-models that define the module's public shape.
- **Implementation/** – Concrete classes that implement those interfaces or extend those bases.
- **Models/** – DTOs, enums, and data-only types (no behavior). Optional; use when the module has clear data types.
- **Events/**, **Enums/**, **Utility/** – Use as needed. Keep feature-specific folders where they logically belong.

### Base types and contract types (Contracts vs Implementation vs Models)

- **Contracts** is for both interfaces and **abstract base / contract types** that other types extend or implement. Examples: `IEventBus`, and an **abstract** `ContextEvent` that all event payloads inherit from. Do not put these in Implementation.
- **Implementation** is only for **concrete** types that implement an interface or extend an abstract type. If a type is the root of a hierarchy (e.g. an abstract base class or record), place it in **Contracts** (or **Models** if it is purely a data shape with no behavioral contract).
- **Models** is for data/shape types: DTOs, records used as payloads, enums. An empty or data-only base type (e.g. a record that only exists as a constraint base) can live in **Contracts** (as a contract) or **Models** (as a data shape); prefer **Contracts** when the type is the central contract of the module’s API.

Summary: abstract base types and contract types belong in **Contracts** (or **Models** when they are purely data). Only concrete implementations go in **Implementation**.

## Naming: Container and Installer

- Classes that inherit from **Container** must be named **`[module]Container`** (e.g. `EventsContainer`, `NavigationContainer`, `SampleGameContainer`).
- Classes that inherit from **Installer** must be named **`[module]Installer`** (e.g. `EventsInstaller`, `NavigationInstaller`).

## Visibility: installers

Any class that inherits from **Installer** and is not abstract must be **public**. Do not make concrete installer types `internal`; the application layer must reference them to register modules with the DI container.

## Unity .meta files

Whenever you **delete** or **move** a file, also delete or move the corresponding `.meta` file (e.g. `Foo.cs` and `Foo.cs.meta`). Unity uses the `.meta` file to store the asset GUID; leaving a stale `.meta` or moving only the asset breaks references (prefabs, scenes, asmdefs).
