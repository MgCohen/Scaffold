# com.scaffold.ugs

# Scaffold UGS

## TL;DR

- Purpose: initialize Unity Gaming Services (UGS) Core and perform anonymous authentication during scoped startup, before other services that depend on UGS identity.
- Location: `Assets/Packages/com.scaffold.ugs/Runtime/Ugs.cs` and `Container/UgsInstaller.cs`.
- Depends on: `Scaffold.Scope` (for `IAsyncLayerInitializable`), `Unity.Services.Core`, `Unity.Services.Authentication`, `VContainer`.
- Used by: bootstrap infra layer (`BootstrapInfraInstaller` registers `UgsInstaller` alongside other infra installers).
- Runtime/Editor: runtime-only service registration and async initialization.
- Keywords: Unity Gaming Services, anonymous sign-in, IAsyncLayerInitializable, UnityServices.

## Responsibilities

- Owns `Ugs` (`IAsyncLayerInitializable`) that ensures `UnityServices` is initialized and the player is signed in anonymously when no session exists.
- Owns `UgsInstaller` registering `Ugs` as singleton and as `IAsyncLayerInitializable`.
- Does not own Cloud Code, Economy, or other UGS product APIs—those belong in their own packages/installers.
- Does not own account linking, platform credentials, or custom authentication UI (out of scope for this minimal module).

## Public API

| Symbol | Purpose | Inputs | Outputs | Failure behavior |
|---|---|---|---|---|
| `Ugs.InitializeAsync` | Entry from `IAsyncLayerInitializable` | `IObjectResolver`, `CancellationToken` | completed task | throws `ArgumentNullException` if resolver is null; propagates UGS/auth exceptions |
| `Ugs` (class) | Holds initialization policy | DI-owned singleton | n/a | n/a |
| `UgsInstaller.Install` | Registers `Ugs` in VContainer | `IContainerBuilder` | n/a | n/a |

**Runtime behavior** (`InitializeAsync` delegates to internal initialization):

1. If `UnityServices.State` is `Uninitialized`, await `UnityServices.InitializeAsync()`.
2. If `AuthenticationService.Instance.IsSignedIn` is false, await `AuthenticationService.Instance.SignInAnonymouslyAsync()`.

If services are already initialized and the user is signed in, calls complete without redundant work.

## Setup / Integration

1. Ensure the project includes **Unity Services Core** and **Authentication** packages (and UGS project/linking in the Services dashboard) so `Unity.Services.Core` and `Unity.Services.Authentication` resolve.
2. Reference `Scaffold.Ugs` and, for composition roots, `Scaffold.Ugs.Container`.
3. Register `UgsInstaller` in the infra layer (see `BootstrapInfraInstaller` in `com.scaffold.bootstrap`).
4. Ensure your `LayeredScope` pipeline runs `IAsyncLayerInitializable` instances so `Ugs` executes before modules that assume UGS is ready.

**Common mistakes**

- Registering Cloud Code or other UGS features without running `Ugs` first in the same startup phase.
- Expecting platform-specific login—this module only performs **anonymous** sign-in when not signed in.

## How to Use

1. Add `UgsInstaller` to the container builder for the scope that should own UGS initialization (typically shared infra).
2. Let the scope base class resolve all `IAsyncLayerInitializable` services during layer startup (same pattern as `AddressablesGateway`, `LiveOpsService`, etc.).
3. Consume Unity Authentication / other UGS APIs only after that layer has finished initializing.
4. If you need a different auth policy (Steam, Apple, etc.), replace or extend this module’s behavior in a dedicated installer rather than patching `Ugs` ad hoc.

## Examples

### Integration (conceptual)

`BootstrapInfraInstaller` already composes infra installers:

```csharp
// From BootstrapInfraInstaller — pattern only
UgsInstaller ugsInstaller = new UgsInstaller();
Install(builder, ugsInstaller);
```

### Minimal consumer

After infra initialization completes, other services may call Authentication or other UGS APIs; this module only guarantees Core + anonymous auth as implemented in `Ugs.cs`.

### Guard / error path

```csharp
// Cancellation during InitializeAsync: passed token is observed before/after awaits;
// do not dispose UnityServices on partial failure without project-specific recovery logic.
```

## Best Practices

- Keep a single `Ugs` singleton per composition root to avoid duplicate initialization races.
- Run UGS initialization in the same async layer as other networked infra (events, navigation) per your `Architecture.md` startup ordering.
- Monitor Unity Dashboard configuration (project ID, services enabled) when anonymous sign-in fails in builds.
- For production, plan explicit auth flows separately from this anonymous baseline.

## Anti-Patterns

- Assuming anonymous identity is sufficient for all LiveOps or multiplayer scenarios without reviewing product requirements.
- Calling `UnityServices.InitializeAsync` manually in scattered places instead of centralizing on `Ugs` (or a single replacement initializer).

## Testing

- Test assembly: `Scaffold.Ugs.Tests` exists but contains no test fixtures yet; add Edit Mode tests when automated UGS coverage is introduced (typically behind Unity Test Framework + Services mocks or integration tests).
- When tests exist, run:

```powershell
& ".\.agents\scripts\run-editmode-tests.ps1" -AssemblyNames "Scaffold.Ugs.Tests"
```

- Bugfix rule: add a regression test when you add coverage; until then, document manual verification steps in the PR.

## AI Agent Context

- Invariants:
  - `Ugs` must remain safe to call when Unity Services are already initialized (no duplicate `InitializeAsync` without checking state).
  - anonymous sign-in only runs when `IsSignedIn` is false.
- Allowed Dependencies:
  - `Scaffold.Scope`, Unity Services Authentication/Core packages, VContainer.
- Forbidden Dependencies:
  - do not reference Cloud Code, LiveOps, or gameplay assemblies from `Scaffold.Ugs` runtime code.
- Change Checklist:
  - verify `BootstrapInfraInstaller` (or consumer) still registers `UgsInstaller` before dependent services.
  - confirm async initializer ordering with `LayerInstallerBase` expectations.
- Known Tricky Areas:
  - UGS dashboard and package versions must match; failures often surface as runtime exceptions inside `InitializeAsync`.

## Related

- `../../../Architecture.md`
- `../com.scaffold.bootstrap/README.md`
- `../com.scaffold.scope/README.md`
- `../../../Docs/Testing/Testing.md`

## Changelog

- `2026-03-31`: Initial README documenting Core initialization, anonymous authentication, and DI integration.
