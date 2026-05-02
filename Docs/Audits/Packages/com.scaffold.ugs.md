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

## 9. Consumers

`Scaffold.Ugs.Ugs` has **zero typed consumers**. The class has no public API beyond `IAsyncInitializable.InitializeAsync`; nothing in the project calls `UnityServices.InitializeAsync` or `SignInAnonymouslyAsync` directly. Its only contract is "I run during the AppFlow init wave". That is consistent with the audit's "thin / OK" verdict.

- `Assets/Packages/com.scaffold.ugs/Container/UgsInstaller.cs:1-` — registers `Ugs` as singleton + `IAsyncInitializable`. Smell: composition root must remember to install this *first* so `CloudCodeService.Instance.CallEndpointAsync(...)` (called transitively from `LiveOpsService`) sees an authenticated session. There is no compile-time enforcement.
- No `[Inject] Ugs ugs;` site exists in `Assets/Packages/com.scaffold.*`, `GameModule/`, or `LiveOps/`. AppFlow stage: `IAsyncInitializable` runs in the host's init wave; the call sequence is implicit (UGS → CloudCode → LiveOps) and depends on installer registration order. No `AppFlow` layer pins the order today.
- Direct callers of `UnityServices.InitializeAsync` / `AuthenticationService.Instance.SignInAnonymouslyAsync` outside the package: zero. Good — confirms that the wrapper is the single point of UGS bootstrap.
- `CloudCodeService` (the Scaffold one) and `LiveOpsService` both **assume UGS is up**; neither checks `UnityServices.State` nor `IsSignedIn` before issuing calls. If a consumer composes installers in the wrong order, the failure surfaces as an opaque Cloud Code SDK exception, not a clear "UGS not initialized" error. This is the strongest argument for adding the `RegisterBuildCallback` validation suggested in the LiveOps audit.
- Test consumers: `Assets/Packages/com.scaffold.ugs/Tests/` declares an asmdef with no fixtures — no production or test consumer paths. A `bootstrapper` test would normally inject `Ugs` and assert idempotency, but no such test exists.

Net: the package is correct in shape and currently uncontroversial because nothing depends on it explicitly. The day a feature needs Apple/Steam sign-in or a non-default environment, the lack of a strategy seam will force a rewrite of every composition root.

## 10. Alternatives & prior art

- **Unity Authentication SDK direct (no wrapper).** `AuthenticationService.Instance.SignInAnonymouslyAsync()` plus `InitializationOptions.SetEnvironmentName` / `SetProfile`. https://docs.unity.com/ugs/manual/authentication/manual/get-started. **Wrap.** The package's value is `IAsyncInitializable` integration + idempotency; the wrapper is justified. Just expose `UgsOptions` and an `IUgsAuthStrategy` (audit §5.1).
- **PlayFab SDK (`LoginWithCustomID`, `LoginWithIOSDeviceID`, etc.).** Per-provider login methods; the consumer composes the chain. https://learn.microsoft.com/gaming/playfab/sdks/unity3d/. **Steal pattern.** PlayFab's per-provider explicit-login is exactly the strategy pattern proposed. Don't adopt the SDK; copy the shape.
- **Firebase Auth.** `SignInWithEmailAndPasswordAsync`, `SignInWithCredentialAsync(googleCredential)`. https://firebase.google.com/docs/auth/unity/start. **Steal pattern.** The credential abstraction (`AuthCredential` union over Google/Apple/Email) is a cleaner generalization than provider-per-method; if Scaffold ends up with 3+ providers, this is the better target shape.
- **Microsoft Authentication Library (MSAL) for .NET.** `IPublicClientApplication` with builder + token cache. https://learn.microsoft.com/entra/msal/dotnet/. **Build (don't adopt).** MSAL is overkill for game UGS; the *builder + cache + clear-on-launch* mental model is worth borrowing for `UgsOptions.ClearSessionOnLaunch`.
- **Unity Sign in with Apple package + Google Play Games plugin.** Per-platform native auth that produces a credential `Ugs` then exchanges. https://docs.unity.com/ugs/manual/authentication/manual/platform-signin. **Wrap (each in its own `IUgsAuthStrategy` impl).** This is the natural extension once the strategy seam exists.

## 11. Benchmark plan

- **`Initialize()` cold-start latency.** What to measure: wall-clock time from `Ugs.InitializeAsync` invocation to completion on first launch (cold) and second invocation (warm/idempotent). Tool: Unity.PerformanceTesting + a play-mode test (real UGS calls) gated by env var, plus an EditMode test using a fake `IUnityServices`. Test location: `com.scaffold.ugs/Tests/InitializeColdStartTests.cs`. Scenario: cold = `UnityServices.State == Uninitialized` and `IsSignedIn == false`; warm = both already true. Baseline: warm path is < 1 ms (audit §3 idempotency claim); cold is network-bound, no expectation. Success: warm path stays under 1 ms across 1000 iterations.
- **`SignInAnonymouslyAsync` failure-retry behavior (correctness).** What to measure: that a transient `AuthenticationException` (e.g., simulated network failure) is *not* swallowed, *not* silently retried, and *is* propagated with project/environment context (audit §4.4). Tool: NUnit EditMode + a fake `IAuthenticationService` interface (currently there isn't one — this benchmark *requires* the §5.1 refactor land first). Test location: `com.scaffold.ugs/Tests/SignInRetryBehaviorTests.cs`. Scenario: fake throws on first call, succeeds on second; assert exception propagates on call 1 (no implicit retry today) and a separate test asserts a future `RetryStrategy` decorator works as documented. Baseline: today, exceptions propagate raw; the test pins that contract.
- **Offline-boot correctness.** What to measure: behavior of `Ugs.InitializeAsync` when `UnityServices.InitializeAsync` throws a network-exception-typed inner exception. Tool: NUnit EditMode + fake. Test location: `com.scaffold.ugs/Tests/OfflineBootTests.cs`. Scenario: fake `IUnityServices` throws a wrapped `RequestFailedException`. Assert that the AppFlow init wave receives a `UgsBootstrapException` (proposed) with project ID + environment in the message, not a raw SDK exception. Baseline: today, raw propagation (audit §4.4). Success: failing test now; passes after the §5.3 wrap lands.
- **Cancellation observability.** What to measure: that a CT cancelled *between* the `InitializeAsync` and `SignInAnonymouslyAsync` calls is observed; that a CT cancelled *during* either call is *not* observed (Unity SDK limitation, audit §4.3). Tool: NUnit EditMode. Test location: `com.scaffold.ugs/Tests/CancellationObservabilityTests.cs`. Scenario: fake `IUnityServices.InitializeAsync` returns a delayed task; cancel CT mid-flight; assert the call completes anyway (proves the SDK limitation), then assert the *next* `ThrowIfCancellationRequested` triggers. Baseline: documents the README's overstated cancellation claim with executable proof.
- **Idempotency of repeated installs.** What to measure: that `UgsInstaller.Install` followed by a second install in a child scope does not re-register `Ugs` such that `InitializeAsync` runs twice. Tool: VContainer integration test. Test location: `com.scaffold.ugs/Tests/InstallerIdempotencyTests.cs`. Scenario: build container, resolve `Ugs`, build child scope with the same installer, resolve again; assert same instance (Lifetime.Singleton parent-scope semantics). Baseline: documents current behavior as a guardrail for the future strategy-pattern installer.

