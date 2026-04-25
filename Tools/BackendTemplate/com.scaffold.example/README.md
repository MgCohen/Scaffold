# com.scaffold.example (backend template)

This folder is a **baseline** for authoring a new LiveOps feature module (server `IGameApiHandler` + DTOs + optional `IGameSetup`).

## How to use

1. Copy `Tools/BackendTemplate/com.scaffold.example` to `Assets/Packages/com.scaffold.<yourfeature>/`.
2. Replace names: `Example` / `example` / `Scaffold.LiveOps.Example` with your feature name (match your package id).
3. Add `Backend~/Scaffold/<YourFeature>/` and `Backend~/Scaffold/<YourFeature.DTO>/` to your real package (this tree mirrors `LiveOps/Scaffold/` after install).
4. Add the two `.csproj` files to `LiveOps/LiveOps.Deploy.sln` (and `LiveOps.sln` if you use tests) with `dotnet sln add`.
5. In the game repo, run **Scaffold > LiveOps > Install or Update Backend** (or `install-liveops-backend.ps1`) to merge all `Backend~/` trees into `LiveOps/`.

`ProjectReference` paths assume the consumer has the standard `LiveOps/` layout (`..\..\Deploy\Core\...` from `LiveOps/Scaffold/<Feature>/`).

## Contents

- `Backend~/Scaffold/Example/` — Cloud Code module (`GameModule` + `IGameApiHandler` + sample `IGameSetup`).
- `Backend~/Scaffold/Example.DTO/` — DTOs with `[LiveOpsKey]` and a sample `ModuleRequest` / `ModuleResponse` pair.

See [Assets/Packages/com.scaffold.liveops/README.md](../../Assets/Packages/com.scaffold.liveops/README.md) and `Docs/Core/LiveOpsKeys.md` for key and manifest rules.