# Application startup (composition root)

There is no `com.scaffold.bootstrap` package in this repository.

Wire your game by **subclassing** [`TwoScopeApplicationHost`](../../Assets/Packages/com.scaffold.scope/Runtime/Host/TwoScopeApplicationHost.cs) in **your** assembly: implement `InstallBaseScope` (e.g. Addressables), `PrepareMainScopeAsync` (preload), and `InstallMainScope` (infra modules, navigation, UGS, scene flow, LiveOps, etc.). See [`Startup-Two-Scope-Preload.md`](../../Plans/Startup-Two-Scope-Preload.md).

Optional loading UI: subscribe to [`ApplicationStartupProgress.Changed`](../../Assets/Packages/com.scaffold.scope/Runtime/Host/ApplicationStartupProgress.cs) on the host’s [`StartupProgress`](../../Assets/Packages/com.scaffold.scope/Runtime/Host/TwoScopeApplicationHost.cs) property, or override [`GetStartupProgressListener`](../../Assets/Packages/com.scaffold.scope/Runtime/Host/TwoScopeApplicationHost.cs) to supply a custom [`IApplicationStartupProgress`](../../Assets/Packages/com.scaffold.scope/Runtime/Contracts/IApplicationStartupProgress.cs).
