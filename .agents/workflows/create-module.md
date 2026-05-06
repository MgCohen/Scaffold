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
    - Create a template interface for cross-module boundary types under `Runtime/Abstraction/` (e.g. `Runtime/Abstraction/I[ModuleName]Service.cs`). Use a flat `Runtime/` if the module is tiny and exposes nothing public; use `Runtime/Abstraction/` + `Runtime/Implementation/` once it does (see `com.scaffold.ads/Runtime/`). The legacy `Runtime/Contracts/` folder name is no longer used in new modules.
    - Create a template installer in `Container/[ModuleName]Installer.cs` implementing `VContainer.IInstaller` (public, `sealed` preferred). See `com.scaffold.ads/Container/AdsInstaller.cs` and `com.scaffold.liveops/Container/LiveOpsInstaller.cs` for the shape.
7.  **(deprecated)** No `[ModuleName]Container.cs` is needed — first-party modules expose only an `IInstaller` (see step 6). The old `Container` base class is gone; do not introduce a new one.
8.  **Documentation Update**:
    - Add authoritative module documentation to `Assets/Packages/<packageId>/README.md` and a short pointer under `Docs/` that links to it, following [`Docs/Standards/Module-Documentation-Standard.md`](../../Docs/Standards/Module-Documentation-Standard.md).
    - Check for circular dependencies.
9.  **Optional Cloud Code backend (`Backend~/`)**:
    - If the module includes a `Scaffold.LiveOps.<Feature>` host slice, copy `Tools/BackendTemplate/com.scaffold.example/Backend~/` into `[ModulePath]/Backend~/` and rename `Example` → `<Feature>` everywhere (folders, csproj names, `AssemblyName`, namespaces `LiveOps.Modules.{Example|Example.DTO}`, `[LiveOpsKey("…")]` values).
    - Mirror the renamed tree to `LiveOps/Scaffold/<Feature>/` and `LiveOps/Scaffold/<Feature>.DTO/` (the `Backend~/` copy under the package is a snapshot — `LiveOps/` is the source of truth while developing in this repo).
    - You do **not** need to hand-edit `LiveOps/LiveOps.Deploy.sln`: **Install or Update Backend** auto-adds discovered csprojs (`MapCsprojToSolutionFolder` → `dotnet sln add`), and the deploy build globs every `LiveOps/Scaffold/**/*.csproj` via `LiveOps/Deploy/Build/Scaffold.LiveOps.Deploy.targets`.
    - Run `pwsh -File .agents/scripts/refresh-liveops-template.ps1` to push `LiveOps/` into the package's `Backend~/` after each change.
    - Full walkthrough (View + ViewModel + ClientService + endpoints + keys): [`Docs/Standards/Module-Vertical-Slice.md`](../../Docs/Standards/Module-Vertical-Slice.md).
10.  **Boundary Hygiene (Best Practice)**:
    - Keep cross-module API types in `Runtime/Abstraction/` and concrete logic in `Runtime/Implementation/` (or flat `Runtime/` for tiny modules).
    - Default non-boundary classes to `internal`.
    - Default external dependencies to `<Module>`; reserve foreign runtime-only dependencies for composition roots (your application startup / `AppFlowRoot` subclass) and module-local wiring.
