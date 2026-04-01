---
description: Scaffolds a new module following the project's structure guidelines.
---

1.  **Determine Module Path**: Ask the user where the module should be created. For publishable first-party modules, default to **`Assets/Packages/com.scaffold.<short-name>/`** (UPM layout with `package.json` at the package root). Legacy examples under `Assets/Scripts/` are no longer the default in this repository.
2.  **Verify GUID Preservation**: Remind the agent/user that `.meta` files are critical for GUID preservation if moving folders.
3.  **Create Top-Level Folders**: Create the following directory structure:
    - `[ModulePath]/Runtime`
    - `[ModulePath]/Container`
    - `[ModulePath]/Tests`
    - `[ModulePath]/Editor` (Optional)
    - `[ModulePath]/Assets` (Optional)
    - `[ModulePath]/Samples` (Optional)
4.  **Resolve Project Prefix (Deterministic Rule)**:
    - Use `scaffold.SCA3001.root_namespace` from `.editorconfig` if present.
    - Else use the repository's `RootNamespace`/project naming conventions.
    - Else fallback to the current assembly prefix pattern already used in this repository.
5.  **Generate Assembly Definitions**: Create `.asmdef` files:
    - `[ModulePath]/Runtime/[ProjectPrefix].[ModuleName].asmdef`
    - `[ModulePath]/Container/[ProjectPrefix].[ModuleName].Container.asmdef` (Reference Runtime module as needed)
    - `[ModulePath]/Tests/[ProjectPrefix].[ModuleName].Tests.asmdef` (Reference Runtime, Container, and Test Framework as needed)
    - Never place the main module asmdef at `[ModulePath]/`; it must live under `[ModulePath]/Runtime/`.
6.  **Generate Initial Boundary API**:
    - Create a template interface in `Runtime/Contracts/I[ModuleName].cs` when the module exposes cross-module boundary types.
    - Create a template installer in `Container/[ModuleName]Installer.cs` (Inheriting from `Installer` and public).
7.  **Create Container**:
    - Create `Container/[ModuleName]Container.cs` (Inheriting from `Container`).
8.  **Documentation Update**:
    - Add or update module documentation under `Docs/` following repository module-doc conventions.
    - Check for circular dependencies.
9.  **Boundary Hygiene (Best Practice)**:
    - Keep cross-module API types in `Runtime/Contracts` and concrete logic in `Runtime`.
    - Default non-boundary classes to `internal`.
    - Default external dependencies to `<Module>`; reserve foreign runtime-only dependencies for composition roots (for example `App/Bootstrap`) and module-local wiring.
