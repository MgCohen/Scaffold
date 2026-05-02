# Audit: com.scaffold.ugs

## 1. Summary & Verdict

`com.scaffold.ugs` is a 2-file package: `Runtime/Ugs.cs` initializes UGS Core then signs in anonymously, and `Container/UgsInstaller.cs` registers it as a singleton + `IAsyncInitializable`. The README is far longer than the implementation and accurately describes the contract. The code is correct for its scope but is right at the line where minimum-code becomes too-minimal: there is no abstraction over auth (Steam, Apple, Google, custom), no environment selection, no profile/session settings, no error narrative, and no test infrastructure beyond a declared test assembly with zero fixtures.

For a backend integration point — exactly what the architect's rubric calls out as a place where good abstraction matters — this is the bare minimum. As long as the project ships with anonymous-only and a single environment, it's fine. As soon as a second auth provider lands, this package needs an `IAuthProvider` seam.

Verdict: **keep, refactor lightly**. The shape is right; flesh out the auth abstraction and add an options object so consumers can pick environment and profile.

## 2. Structure

```
com.scaffold.ugs/
  package.json                                       ; deps: com.scaffold.appflow only
  README.md                                          ; thorough; ~120 lines for ~30 lines of code
  Container/
    UgsInstaller.cs                                  ; Ugs -> singleton + IAsyncInitializable
  Runtime/
    Ugs.cs                                           ; InitializeAsync: UnityServices.InitializeAsync + SignInAnonymouslyAsync
  Tests/                                             ; (empty; declared, no fixtures)
```

No `Backend~/`. No editor tooling. No DTOs. No platform-conditional code.

The `package.json` declares only `com.scaffold.appflow` (`com.scaffold.ugs/package.json:13`). The runtime references `Unity.Services.Authentication`, `Unity.Services.Core`, and VContainer (`Runtime/Ugs.cs:4-5`, `Container/UgsInstaller.cs:1-3`). These are not declared dependencies — Unity will resolve them transitively from the project manifest, but it's not self-describing.

## 3. What's Good

- **Idempotent.** `Ugs.EnsureInitializedAsync` (`Runtime/Ugs.cs:16-31`) checks `UnityServices.State` and `IsSignedIn` before doing the work. Re-entry is safe.
- **AppFlow integration.** `IAsyncInitializable` lets the host run UGS init in the right phase without a custom bootstrap — same idiom as `LiveOpsService`, `PushSubscriptionService`, `PushDisconnectHandler`.
- **Single-purpose package.** It does exactly what the README says: Core init + anonymous sign-in. No scope creep.
- **Cancellation observed at start and between awaits.** `cancellationToken.ThrowIfCancellationRequested()` at `Runtime/Ugs.cs:18,25`.
- **No null guards in the constructor / installer.** Per the architect's rubric — none needed, none added. Good.
- **Sealed class, sealed installer.** `Runtime/Ugs.cs:9`, `Container/UgsInstaller.cs:7`. Proper.
- **README is honest about scope.** "anonymous sign-in only" / "Forbidden Dependencies: do not reference Cloud Code, LiveOps, or gameplay assemblies" (`README.md:103-107`). Sets expectations.

## 4. Issues / Smells

### 4.1 No auth provider abstraction

The architect's rubric: *"Backend integration points are exactly where good abstraction matters."* This **is** an auth integration point. The class hard-codes `AuthenticationService.Instance.SignInAnonymouslyAsync()` (`Runtime/Ugs.cs:29`). When the project needs Sign in with Apple or Steam, the only paths are:

1. Replace this package, or
2. Edit `Ugs.cs` directly.

Both are wrong. An `IUgsAuthStrategy` with `Task SignInAsync(CancellationToken ct)` would let `AnonymousSignIn` ship as the default and let consumers register, e.g., `ApplePlatformSignIn` instead.

### 4.2 No environment / profile control

Unity Authentication 3.x supports `InitializationOptions` (environment name, custom profile). The package ignores both and calls the parameterless overloads (`Runtime/Ugs.cs:22,29`). For projects that need:

- Multiple environments (dev/staging/prod) — typical for LiveOps,
- Per-tester profiles to avoid sharing anonymous identities — common in QA,

this package is a roadblock. A `UgsOptions` POCO injected via DI would solve both.

### 4.3 Cancellation does not interrupt the awaited Unity calls

`UnityServices.InitializeAsync()` (`Runtime/Ugs.cs:22`) and `AuthenticationService.Instance.SignInAnonymouslyAsync()` (`Runtime/Ugs.cs:29`) do not accept a `CancellationToken`. The token is checked between calls but cannot abort an in-flight request. This is a Unity SDK limitation, not the package's fault, but the README's claim *"Cancellation during InitializeAsync: passed token is observed before/after awaits"* (`README.md:73`) should make the limitation explicit.

### 4.4 No retry / no error narrative

Auth in builds fails for many reasons (clock skew, network, COPPA region restrictions, dashboard misconfig). The README says *"propagates UGS/auth exceptions"* (`README.md:25`). Propagating is correct, but the AppFlow init wave will then surface a raw `AuthenticationException` to the user. Wrap with a single `try/catch` that adds context: dashboard project ID, environment, attempt count.

### 4.5 No tests, despite a Tests folder

`com.scaffold.ugs/Tests/` exists per README (`README.md:88-91`) but contains no fixtures. With Unity's `IAuthenticationService` mockable via the SDK's interface or a wrapper, two tests are warranted:

1. Pre-initialized state: `EnsureInitializedAsync` is a no-op.
2. Already-signed-in: `SignInAnonymouslyAsync` is not called.

### 4.6 README is ~4x the code size

`README.md:1-124` documents an 18-line class. Most of it is copy from a doc template (Allowed/Forbidden Dependencies, Anti-Patterns, Best Practices). For a package this small, the README itself is the work — and it isn't being maintained against the implementation (e.g. `README.md:73` overstates cancellation). Either trim hard or attach the README to a richer feature set.

### 4.7 `package.json` under-declares dependencies

`com.scaffold.ugs/package.json:13` declares only `com.scaffold.appflow`. The runtime needs `Unity.Services.Core`, `Unity.Services.Authentication`, and VContainer. README references them (`README.md:8-9`) but `package.json` does not. Add them.

### 4.8 No tests asmdef shown

`Tests/` exists but no `.asmdef` was included in the audit listing. Either remove the empty folder or commit the asmdef + at least one stub test.

### 4.9 The class name `Ugs` is a noun, not a service role

`Scaffold.Ugs.Ugs` is awkward. The class implements an initialization policy; rename to `UgsBootstrap` or `UgsInitializer`. Today calling `container.Resolve<Ugs>()` reads as "give me the UGS facade" but the class has no API beyond `InitializeAsync`.

## 5. Suggested Before/After Snippets

### 5.1 Auth strategy abstraction

Before (`Runtime/Ugs.cs:9-32`):

```csharp
public sealed class Ugs : IAsyncInitializable
{
    public Task InitializeAsync(CancellationToken cancellationToken)
        => EnsureInitializedAsync(cancellationToken);

    internal async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (UnityServices.State is ServicesInitializationState.Uninitialized)
            await UnityServices.InitializeAsync();
        cancellationToken.ThrowIfCancellationRequested();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }
}
```

After:

```csharp
public interface IUgsAuthStrategy
{
    Task SignInAsync(CancellationToken cancellationToken);
}

public sealed class AnonymousAuthStrategy : IUgsAuthStrategy
{
    public Task SignInAsync(CancellationToken cancellationToken)
        => AuthenticationService.Instance.IsSignedIn
            ? Task.CompletedTask
            : AuthenticationService.Instance.SignInAnonymouslyAsync();
}

public sealed record UgsOptions(string EnvironmentName = null, string Profile = null);

public sealed class UgsBootstrap : IAsyncInitializable
{
    private readonly IUgsAuthStrategy auth;
    private readonly UgsOptions options;

    public UgsBootstrap(IUgsAuthStrategy auth, UgsOptions options)
    {
        this.auth = auth;
        this.options = options;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (UnityServices.State is ServicesInitializationState.Uninitialized)
        {
            InitializationOptions init = new();
            if (!string.IsNullOrEmpty(options.EnvironmentName)) init.SetEnvironmentName(options.EnvironmentName);
            if (!string.IsNullOrEmpty(options.Profile)) init.SetProfile(options.Profile);
            await UnityServices.InitializeAsync(init);
        }
        cancellationToken.ThrowIfCancellationRequested();
        await auth.SignInAsync(cancellationToken);
    }
}
```

### 5.2 Installer with a default strategy

```csharp
public sealed class UgsInstaller : IInstaller
{
    private readonly UgsOptions options;
    public UgsInstaller(UgsOptions options = null) { this.options = options ?? new UgsOptions(); }

    public void Install(IContainerBuilder builder)
    {
        builder.RegisterInstance(options);
        // Consumer can override by registering IUgsAuthStrategy before this installer.
        if (!builder.Exists(typeof(IUgsAuthStrategy), findParentScopes: false))
            builder.Register<IUgsAuthStrategy, AnonymousAuthStrategy>(Lifetime.Singleton);
        builder.Register<UgsBootstrap>(Lifetime.Singleton)
            .AsSelf()
            .As<IAsyncInitializable>();
    }
}
```

(`builder.Exists` is approximate — VContainer doesn't have it; use a flag or always register and document precedence.)

### 5.3 Error narrative

```csharp
catch (AuthenticationException ex)
{
    throw new InvalidOperationException(
        $"UGS anonymous sign-in failed (project='{Application.cloudProjectId}', env='{options.EnvironmentName ?? "<default>"}'). " +
        $"Verify Edit > Project Settings > Services and the dashboard auth provider is enabled.", ex);
}
```

## 6. Easy Wins

1. Rename `Ugs` to `UgsBootstrap` (`Runtime/Ugs.cs:9`).
2. Add `Unity.Services.Core`, `Unity.Services.Authentication`, `jp.hadashikick.vcontainer` to `package.json` (`com.scaffold.ugs/package.json:12-15`).
3. Trim `README.md` to ~30 lines that match the code; remove sections that don't apply (e.g., "Examples" and "Anti-Patterns" that fit a larger feature). Or expand the code first.
4. Wrap the auth call in a try/catch that adds project + environment context (`Runtime/Ugs.cs:29`).
5. Either remove `Tests/` or add the missing asmdef + a single `EnsureInitialized_NoOpWhenSignedIn` test.
6. Wire `InitializationOptions.SetEnvironmentName` from a `UgsOptions` POCO so dev/staging/prod can be selected.
7. Wire `SetProfile` for QA isolation.
8. Add an `IUgsAuthStrategy` so anonymous is the default but Apple/Steam are pluggable.

## 7. Bigger Refactors

- **Auth strategy + provider chain.** Today's package == "anonymous only". A typical UGS project ends up needing: anonymous-then-link-platform, multiple platform SDKs, custom server token. Build the strategy seam now (one interface, one default class) so consumers don't wedge platform code into `Ugs.cs`.
- **Environment routing.** `InitializationOptions.SetEnvironmentName` must come from somewhere — typically a build-time constant or a debug menu. A `UgsEnvironmentProvider` (interface) lets QA flip envs at runtime without rebuilding.
- **Session persistence policy.** Unity's `AuthenticationService` persists session tokens by default; for QA you may want to clear on launch. A `bool ClearSessionOnLaunch` option in `UgsOptions` would handle it explicitly.

## 8. Organization & Docs

- **README is over-documented vs. implementation.** Either grow the implementation to the README's shape (auth strategy, options, env routing) or shrink the README. Today the imbalance is misleading.
- **Naming.** `Scaffold.Ugs.Ugs` (`Runtime/Ugs.cs:9`) is a tongue-twister; rename.
- **Tests folder is empty.** Remove or fill.
- **No `AssemblyInfo.cs`.** Other packages (`com.scaffold.cloudcode`, `com.scaffold.liveops`) have one with `InternalsVisibleTo`. Add for consistency once `EnsureInitializedAsync` becomes `internal`-only.
- **License/author fields**: present in `package.json`. Good.

### References

- Unity Authentication 3.x — `InitializationOptions`, `SetEnvironmentName`, `SetProfile`: https://docs.unity.com/ugs/manual/authentication/manual/get-started
- Unity Sign in with Apple / Google / Steam — provider matrix: https://docs.unity.com/ugs/manual/authentication/manual/platform-signin
- AppFlow `IAsyncInitializable` (in-repo, `com.scaffold.appflow`) — keep auth in the same async wave as other infra.
- Firebase Auth — strategy pattern reference (`SignInWithEmailAndPasswordAsync`, `SignInWithCredentialAsync`): https://firebase.google.com/docs/auth/unity/start
- PlayFab `LoginWithCustomID` / `LoginWithIOSDeviceID` — separate calls per provider; the strategy pattern keeps them swappable.
