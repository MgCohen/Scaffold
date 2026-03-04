---
description: Scaffolds a new module following the project's structure guidelines.
---

1.  **Determine Module Path**: Ask the user where the module should be created (e.g., `Assets/Scripts/Infra/[ModuleName]`).
2.  **Verify GUID Preservation**: Remind the agent/user that `.meta` files are critical for GUID preservation if moving folders.
3.  **Create Top-Level Folders**: Create the following directory structure:
    - `[ModulePath]/Runtime`
    - `[ModulePath]/Runtime/Contracts`
    - `[ModulePath]/Runtime/Implementation`
    - `[ModulePath]/Container`
    - `[ModulePath]/Tests`
    - `[ModulePath]/Editor` (Optional)
    - `[ModulePath]/Assets` (Optional)
    - `[ModulePath]/Samples` (Optional)
4.  **Generate Assembly Definitions**: Create `.asmdef` files:
    - `[ModulePath]/Runtime/Scaffold.[ModuleName].asmdef`
    - `[ModulePath]/Container/Scaffold.[ModuleName].Container.asmdef` (Reference Runtime)
    - `[ModulePath]/Tests/Scaffold.[ModuleName].Tests.asmdef` (Reference Runtime, Container, and Test Framework)
5.  **Generate Initial Contracts**:
    - Create a template interface in `Runtime/Contracts/I[ModuleName].cs`.
    - Create a template installer in `Container/[ModuleName]Installer.cs` (Inheriting from `Installer` and public).
6.  **Create Container**:
    - Create `Container/[ModuleName]Container.cs` (Inheriting from `Container`).
7.  **Documentation Update**:
    - Remind the user to update `Assets/Scripts/Infra/infra-module-analysis.md` (or the correct path once verified) with the new module details.
    - Check for circular dependencies.
