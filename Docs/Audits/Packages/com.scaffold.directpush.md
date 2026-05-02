# Audit: com.scaffold.directpush

## 1. Summary & Verdict

`com.scaffold.directpush` is a thin wrapper around Unity Cloud Code's `SubscribeToPlayerMessagesAsync` / `SubscribeToProjectMessagesAsync` for receiving pushes, plus a `DirectPushClient` that emits typed `ModuleRequest<SendPushResponse>` envelopes through `ILiveOpsService`. It ships a `Backend~/` tree with three Cloud Code handlers (`SendSelfPushRequest`, `SendPlayerPushRequest`, `SendProjectPushRequest`) and a server-auth GUID check via `IServerAuth.IsValidForServerAccessAsync`.

Verdict: **refactor**. The send-side is in good shape — typed DTOs, shared with the server, server auth. The receive-side is the problem: handlers are `Action` (no payload), payloads are dropped on the floor (`PushSubscriptionService.cs:68`), there is no abstraction over the push channel (Cloud Code is hard-coded), the message router is a `Dictionary<string, List<Action>>` keyed by string, and there is no offline buffering, no replay/dedup, no cancellation, and no `Unsubscribe`. For a feature that the server can fire arbitrarily, this is below the bar.

## 2. Structure

```text
com.scaffold.directpush/
  package.json                                       ; deps: VContainer only (see 8.3)
  README.md                                          ; current docs match implementation
  Container/
    DirectPushInstaller.cs                           ; registers PushSubscriptionService + DirectPushClient + PushDisconnectHandler
  Runtime/
    PushSubscriptionService.cs                       ; subscribes to Unity SDK, dispatches by string
    DirectPushClient.cs                              ; sends pushes via ILiveOpsService
    PushDisconnectHandler.cs                         ; sample handler that quits the app
  Backend~/
    Scaffold/
      DirectPush.DTO/
        PushToPlayerKeys.cs                          ; const string keys
        PushToProjectKeys.cs                         ; const string keys
        Request/
          SendPlayerPushRequest.cs                   ; [LiveOpsKey] DTO
          SendProjectPushRequest.cs                  ; [LiveOpsKey] DTO
          SendPushResponse.cs                        ; ModuleResponse
          SendSelfPushRequest.cs                     ; [LiveOpsKey] DTO
      DirectPush/
        DirectPushPlayerPushHandler.cs               ; IGameApiHandler<SendPlayerPushRequest, SendPushResponse>
        DirectPushProjectPushHandler.cs
        DirectPushSelfPushHandler.cs
```

DTOs are in `Backend~/.../DTO/` and consumed both by the server handlers (`Backend~/.../DirectPush/`) and the client (`Runtime/DirectPushClient.cs:3-4` references `LiveOps.Modules.DTO.ModuleRequests`). They are precompiled into `Assets/Plugins/Scaffold.LiveOps.DTO/` per the LiveOps build pipeline; the client and server compile against the same `*.DTO.dll`. Cross-side DTO sharing: yes, by build target.

## 3. What's Good

- **Send-side is typed and shared.** `SendSelfPushRequest`, `SendPlayerPushRequest`, `SendProjectPushRequest` (`Backend~/.../Request/*.cs`) extend `ModuleRequest<SendPushResponse>` and carry `[LiveOpsKey("...")]`. The client (`DirectPushClient.cs:18-53`) just news them up and calls `liveOpsService.CallAsync(request, ct)` — typed in, typed out, server compiles against the identical class.
- **Server auth on player/project pushes.** Both `DirectPushPlayerPushHandler.cs:35-43` and `DirectPushProjectPushHandler.cs:35-43` gate on `IServerAuth.IsValidForServerAccessAsync` with a stored GUID. `IsValidForServerAccessAsync` uses `CryptographicOperations.FixedTimeEquals` (`Backend~/.../ServerAuth/GameStateServerAuth.cs:51`) — proper constant-time compare, not `string.Equals`.
- **DI cleanliness.** `DirectPushInstaller.cs` registers three singletons; both `PushSubscriptionService` and `PushDisconnectHandler` participate in `IAsyncInitializable` so they hydrate during the AppFlow init wave.
- **Disconnect handler is a real example.** `PushDisconnectHandler.cs` shows the receive pattern: subscribe in `InitializeAsync`, react. It also models the dual subscription (player + project) cleanly.

## 4. Issues / Smells

### 4.1 Push handlers receive `Action`, not the payload

`PushSubscriptionService.SubscribeToPlayerMessage(string messageType, Action handler)` (`Runtime/PushSubscriptionService.cs:18`). The Unity SDK delivers a `MessageReceived` event with `@event.Message` (the JSON body) and `@event.MessageType`. The service logs the type and **drops the body** (`Runtime/PushSubscriptionService.cs:68,78`). There is no way to get the payload to a handler. For a "direct push" feature this is the central capability, missing.

### 4.2 Stringly-typed dispatch with no payload generic

`Dictionary<string, List<Action>> playerHandlers` and `projectHandlers` (`Runtime/PushSubscriptionService.cs:15-16`) and `string` keys re-typed as `const string` in `PushToPlayerKeys.cs` / `PushToProjectKeys.cs`. The architect's rubric says generics for compile-time safety. There is no `IPushMessage<TPayload>`, no typed handler registry, no JSON-binder routing.

### 4.3 No `Unsubscribe`

Handlers are added (`Runtime/PushSubscriptionService.cs:18-38`) and the dictionary is cleared on `Dispose` (line 47-51), but there is no `Unsubscribe(string, Action)` for individual handlers. This makes view-model subscriptions during gameplay impossible without leaks; combined with `Dispose` clearing all handlers, scoped lifetimes break.

### 4.4 No abstraction over the push channel

The package is named `DirectPush` and reads as a generic concept, but it is hard-bound to `Unity.Services.CloudCode.CloudCodeService.Instance.SubscribeToPlayerMessagesAsync` (`Runtime/PushSubscriptionService.cs:56,62`). No `IPushChannel` interface; no test seam; you can't swap to Photon Realtime, SignalR, MQTT, or even a fake. For a backend integration point — exactly the place the architect said abstraction matters — this is missing.

### 4.5 No offline buffering / replay / dedup

The SDK callback fires when connected; if the client is offline or the subscription is `Kicked`, messages are lost (`Runtime/PushSubscriptionService.cs:70`). There is no buffer, no LWW key, no idempotency token. `Disconnect` only logs.

### 4.6 No cancellation through `InitializeAsync`

`InitializeAsync` (`Runtime/PushSubscriptionService.cs:40-45`) calls `cancellationToken.ThrowIfCancellationRequested()` once, then awaits `CloudCodeService.Instance.SubscribeToPlayerMessagesAsync(callbacks)` (`:56`) — note the SDK signature does not take a CT, so cancellation cannot interrupt subscription. Document or wrap.

### 4.7 Ad-hoc dispatcher is unsafe under exceptions

`DispatchHandlers` (`Runtime/PushSubscriptionService.cs:85-94`) iterates and invokes handlers synchronously on the SDK's callback thread. If one handler throws, the rest are skipped. No try/catch boundary, no Unity-main-thread post.

### 4.8 `PushDisconnectHandler.Dispose()` is empty

`Runtime/PushDisconnectHandler.cs:56-58`. Either remove `IDisposable` or actually unsubscribe from `PushSubscriptionService` (you can't, see 4.3).

### 4.9 `Action` quits the editor synchronously

`OnDisconnectReceived` (`Runtime/PushDisconnectHandler.cs:60-69`) calls `Application.Quit()` (or `EditorApplication.isPlaying = false`) on the SDK callback thread. Not main-thread-safe; depending on Unity version this may or may not work. Should be marshalled.

### 4.10 DTO typing weaknesses

- `SendPushResponse` has no payload (`Backend~/.../Request/SendPushResponse.cs:6-12`). Confirmation receipts are common; at minimum carry the `MessageId` so the sender can correlate.
- The three send-DTOs have a duplicated `Message`/`MessageType` pair (`SendSelfPushRequest.cs:8-10`, `SendPlayerPushRequest.cs:8-9`, `SendProjectPushRequest.cs:8-9`). Those should be `string Payload` (or generic `T`) and `string MessageType` shared via a base. Today `Message` is `string` so any consumer must serialize their own JSON.

### 4.11 Server handlers re-build a response object three times

`DirectPushPlayerPushHandler.cs:40-54`, `DirectPushProjectPushHandler.cs:40-54`, `DirectPushSelfPushHandler.cs:30-37` each do:

```csharp
SendPushResponse response = new SendPushResponse();
response.SetResponse(ResponseStatusType.Success, "Player message sent");
return response;
```

`ModuleResponse` should expose static helpers `SendPushResponse.Ok(string msg)` / `Fail(string msg)` so handlers stay one-line.

### 4.12 `MessageType` / wire-key duplication

`PushToPlayerKeys.PushDisconnectMultiplePlayerAccounts = "PushDisconnectMultiplePlayerAccounts"` (`Backend~/.../PushToPlayerKeys.cs:7`). Same string is the message type the server emits and the key the client subscribes with. The pattern is correct, but the `const string` lives in `LiveOps.Modules.DTO.DirectPush`, while the request DTOs use `[LiveOpsKey("SendPlayerPushRequest")]` against `LiveOpsKeyResolver`. Two key conventions in one feature; pick one (prefer `[LiveOpsKey]` + `KeyOf<TPayload>.Wire` for both directions).

### 4.13 Logging spam without sampling

`PushSubscriptionService.cs:68,69,78,79` logs every state change at `Debug.Log`. In a long session with many pushes this produces non-trivial GC pressure (`JsonConvert.SerializeObject` on every error event, `:71`). Gate behind a settings flag like `CloudCodeSettings.LogCalls`.

## 5. Suggested Before/After Snippets

### 5.1 Typed receive

Before (`Runtime/PushSubscriptionService.cs:18-27`):

```csharp
public void SubscribeToPlayerMessage(string messageType, Action handler)
{
    if (!playerHandlers.TryGetValue(messageType, out var handlers)) { ... }
    handlers.Add(handler);
}
```

After (re-using `[LiveOpsKey]` for symmetry with send):

```csharp
public interface IPushPayload { }                                 // marker
public interface IPlayerPushPayload : IPushPayload { }
public interface IProjectPushPayload : IPushPayload { }

public sealed class PushSubscriptionService : IAsyncInitializable, IDisposable
{
    public IDisposable SubscribePlayer<TPayload>(Action<TPayload> handler)
        where TPayload : IPlayerPushPayload
    {
        string key = KeyOf<TPayload>.Wire ?? throw new InvalidOperationException(
            $"{typeof(TPayload)} needs [LiveOpsKey].");
        return SubscribeInternal(playerHandlers, key, handler);
    }

    private static IDisposable SubscribeInternal<T>(
        ConcurrentDictionary<string, List<Action<string>>> table, string key, Action<T> handler)
    {
        Action<string> typed = json => handler(json.FromJson<T>());
        var list = table.GetOrAdd(key, _ => new List<Action<string>>());
        lock (list) list.Add(typed);
        return new DisposableUnsubscribe(list, typed);
    }
}
```

The dispatch path then forwards `@event.Message` (the JSON), not nothing, and routes by `[LiveOpsKey]` wire key against the typed payload.

### 5.2 Channel abstraction

Before: `CloudCodeService.Instance.SubscribeToPlayerMessagesAsync(...)` is hard-wired in `Runtime/PushSubscriptionService.cs:56,62`.

After:

```csharp
public interface IPushChannel
{
    Task SubscribePlayerAsync(SubscriptionEventCallbacks callbacks, CancellationToken ct);
    Task SubscribeProjectAsync(SubscriptionEventCallbacks callbacks, CancellationToken ct);
}

internal sealed class CloudCodePushChannel : IPushChannel { /* uses CloudCodeService.Instance */ }

// DirectPushInstaller registers IPushChannel. Test seam = FakePushChannel.
```

### 5.3 Offline buffer (simple, minimal)

```csharp
private readonly ConcurrentQueue<(string key, string json)> bufferOnDisconnect = new();
// On reconnect (ConnectionStateChanged), drain the queue back through DispatchHandlers.
```

### 5.4 Response factory

```csharp
public abstract class ModuleResponse
{
    public static T Ok<T>(string msg = null) where T : ModuleResponse, new()
    { var r = new T(); r.SetResponse(ResponseStatusType.Success, msg ?? string.Empty); return r; }

    public static T Fail<T>(string msg) where T : ModuleResponse, new()
    { var r = new T(); r.SetResponseFailure(msg); return r; }
}
```

(Lives in `LiveOps.DTO`, not in this package; called out here because of how often the handlers repeat the pattern.)

## 6. Easy Wins

1. Forward `@event.Message` to handlers as a `string` (or pre-deserialize) in `PushSubscriptionService.cs:68,78`.
2. Add `Unsubscribe<TPayload>(Action<TPayload>)` and have `Subscribe` return an `IDisposable` (`Runtime/PushSubscriptionService.cs:18-38`).
3. Wrap dispatch in `try/catch` per-handler to isolate exceptions (`Runtime/PushSubscriptionService.cs:85-94`).
4. Either remove `PushDisconnectHandler : IDisposable` or actually unsubscribe (`Runtime/PushDisconnectHandler.cs:56-58`).
5. Stop double-logging error JSON on every event (`PushSubscriptionService.cs:71,81`); gate behind a settings flag.
6. Add `MessageId` to `SendPushResponse` for client-side correlation (`Backend~/.../Request/SendPushResponse.cs`).
7. Move `Message`/`MessageType` to a `SendPushRequestBase` to deduplicate the three send DTOs.
8. Mark `PushSubscriptionService` `sealed`-and-`internal` and expose `IPushSubscription` as the consumer-facing interface (small but matches the rubric: abstract at the entry point only).

## 7. Bigger Refactors

- **Channel abstraction (`IPushChannel`).** Cloud Code is one of three plausible push backends (Cloud Code subscriptions, Photon Realtime, custom WebSocket). Do this now while there is exactly one implementation; cost is two files, value is the test seam plus future-proofing.
- **Typed payload pipeline.** Use `[LiveOpsKey]` on push-payload DTOs and route by `KeyOf<T>.Wire`. This gives compile-time matching from server emission to client subscription, parity with the send path. Generator already exists (`Scaffold.LiveOps.Bootstrap.Generators` per the host README).
- **Offline buffering & replay.** The Unity SDK reports `ConnectionStateChanged`/`Kicked` but the package only logs (`PushSubscriptionService.cs:69-70`). Buffer last-N messages per type in memory (key-aware: only the latest of each key for state messages, full queue for events). On reconnect, replay.
- **Acknowledgement.** Add an optional `MessageId` echo; the server can dedup retries.

## 8. Organization & Docs

- **`README.md`** is short, accurate to the implementation, and matches the actual API. Keep — but update once payloads are typed.
- **`package.json`** lists only `jp.hadashikick.vcontainer` as a dependency. The runtime references `Scaffold.AppFlow.IAsyncInitializable` (`Runtime/PushSubscriptionService.cs:6`) and `Scaffold.LiveOps.ILiveOpsService` (`Runtime/DirectPushClient.cs:5`); both must be declared deps.
- **No tests.** Compare with `com.scaffold.cloudcode/Tests/` — direct-push has zero coverage. At minimum, test the dispatch table (subscribe → fake-fire → handler invoked) once `Unsubscribe` exists.
- **Backend handlers** (`Backend~/Scaffold/DirectPush/*.cs`) are well structured; only minor: `using LiveOps.ModuleFetchData;` is imported but unused in `DirectPushSelfPushHandler.cs` (no `IServerAuth` here, by design — self-push doesn't need server-auth, but the file imports `LiveOps.ServerAuth` only via the others).
- **No analyzer suppression** — good. The `LiveOps.DTO` JSON binder allowlists `Scaffold.LiveOps.*`/`LiveOps.Modules.*` (`Backend~/Deploy/Core/LiveOps.DTO/Json/CrossPlatformTypeBinder.cs:62-69`) which already covers these DTOs.

### References (push patterns)

- Unity Cloud Code Player Messaging — `SubscribeToPlayerMessagesAsync` SDK pattern: https://docs.unity.com/ugs/manual/cloud-code/manual/messaging
- SignalR `Hub.On<T>` typed handlers: https://learn.microsoft.com/aspnet/core/signalr/hubs
- Firebase Cloud Messaging — `MessageId` for dedup, topic subscription model: https://firebase.google.com/docs/cloud-messaging
- PlayFab Real-time Messaging (deprecated) — relied on string event types; they later moved to typed events. Lesson: don't repeat their first design.
- The LiveOps `[LiveOpsKey]` + `KeyOf<T>` pattern in this same repo (`Backend~/Deploy/Core/LiveOps.DTO/Keys/KeyOf.cs:16-26`) is exactly the typed-routing primitive to reuse here.

## 9. Consumers

`PushSubscriptionService` has **exactly one consumer**: the `PushDisconnectHandler` shipped *inside the same package*. There are zero external subscribers in `Assets/`, `GameModule/`, or `LiveOps/`. The only payload any subscriber actually wants today is "the disconnect happened" — which is why the audit's "handlers are `Action`, no payload" smell hasn't yet bitten in production: there is no production. As soon as a real consumer (e.g., currency-changed push, mail-received push) lands, §4.1 becomes a blocker.

- `Assets/Packages/com.scaffold.directpush/Runtime/PushDisconnectHandler.cs:25-31` — the only `SubscribeToPlayerMessage`/`SubscribeToProjectMessage` call. Hands in a parameterless `OnDisconnectReceived` (`PushDisconnectHandler.cs:60`); discards any payload. Smell: confirms the audit — the only existing subscriber proves the API by accident, because it happens to need nothing more than "fire".
- `Assets/Packages/com.scaffold.directpush/Runtime/DirectPushClient.cs:18-52` — three send sites (`SendSelfPushAsync`, `SendPlayerPushAsync`, `SendProjectPushAsync`) wrapping `liveOpsService.CallAsync(request, ct)`. Each duplicates `Message`/`MessageType` field assignment; consumers would benefit from a higher-level `IPushSender<TPayload>` keyed by `[LiveOpsKey]` (audit §4.10).
- `Assets/Packages/com.scaffold.directpush/Container/DirectPushInstaller.cs:11-15` — registers `PushSubscriptionService` and `DirectPushClient` as singletons, both also bound as `IAsyncInitializable`/`IDisposable`. No consumer composition root references this installer outside the package.
- No `Assets/Packages/com.scaffold.ads*` subscriber, no GameModule subscriber, no LiveOps-side subscriber. Net: 1 subscriber, 1 message type, 0 payload data. The "no offline buffer / no replay / no Unsubscribe" criticisms are correct *and* unobserved because no consumer needs them yet.

## 10. Alternatives & prior art

- **Firebase Cloud Messaging client patterns.** Topic subscriptions, `MessageId` for dedup, foreground/background handler split. https://firebase.google.com/docs/cloud-messaging. **Steal pattern.** Borrow the `MessageId` echo + topic semantics; do not adopt the SDK (Unity already uses Cloud Code messaging).
- **SignalR `Hub.On<T>`.** Typed message dispatch over WebSocket with `IDisposable` subscriptions. https://learn.microsoft.com/aspnet/core/signalr/hubs. **Steal pattern.** Exactly the API shape proposed in §5.1: `IDisposable On<T>(Action<T>)`, payload typed, unsubscribe via `Dispose`.
- **MessagePipe + R3 (Cysharp).** In-process typed pub/sub with key/filter support; `R3` adds reactive operators (buffer, throttle, replay). https://github.com/Cysharp/MessagePipe, https://github.com/Cysharp/R3. **Adopt** for the in-process dispatch table behind `IPushChannel`. MessagePipe gives keyed pub/sub (`IPublisher<TKey,TMessage>`) for free; R3's `Replay` operator is a drop-in answer to the missing offline-buffer concern.
- **Photon Realtime / Mirror Networking.** Game-network message-typed dispatch; `OnEvent<T>` patterns. https://doc.photonengine.com/realtime/current/getting-started/realtime-intro. **Build.** The push channel abstraction (audit §4.4) should be shaped so a Photon implementation is plausible without leaking Photon types.
- **Cloud Code Player Messaging SDK direct.** What the package wraps today. https://docs.unity.com/ugs/manual/cloud-code/manual/messaging. **Wrap.** Confirmed; keep wrapping but expose payload + unsubscribe + replay as the audit recommends.

## 11. Benchmark plan

- **Subscribe/unsubscribe alloc cost.** What to measure: bytes-allocated and ns/op for one subscribe + dispose cycle on a `[LiveOpsKey]`-typed payload (after the §5.1 refactor lands; today, only string-keyed `Action`). Tool: Unity.PerformanceTesting. Test location: `com.scaffold.directpush/Tests/SubscribeBenchmarks.cs`. Scenario: 10k subscribe/dispose cycles; assert no per-op `List<>` re-allocation when the same key is reused. Baseline: today's `List.Add` + dictionary lookup is ~150 ns and zero allocs in steady state. Success: typed version stays within 2x.
- **Backpressure / 1000 queued pushes (correctness + perf).** What to measure: behavior when the dispatch fires 1000 messages in a tight loop on the SDK callback thread before any handler awaits. Tool: NUnit EditMode + Unity.PerformanceTesting. Test location: `com.scaffold.directpush/Tests/DispatchBackpressureTests.cs`. Scenario: fake `IPushChannel` raises 1000 events synchronously; assert no exception on the callback thread, no message lost (proves a need for an internal queue), and dispatch latency p99 < 5 ms per message. Baseline expectation: today, no queue — synchronous handler invocation under SDK callback thread (audit §4.7); throws skip subsequent handlers.
- **Reconnect / replay (correctness — proves the bug).** What to measure: that messages sent while `ConnectionState == Disconnected` are *delivered* on reconnect (audit §4.5). Tool: NUnit EditMode. Test location: `com.scaffold.directpush/Tests/OfflineReplayTests.cs`. Scenario: fake `IPushChannel` raises `ConnectionStateChanged(Disconnected)`, then enqueues 5 events, then raises `ConnectionStateChanged(Connected)`. Assert all 5 reach a subscribed handler. Baseline: today this test fails — currently no replay. Success: failing test pinned; passes once the buffer lands.
- **Per-handler exception isolation (correctness).** What to measure: that one throwing handler doesn't skip subsequent handlers in `DispatchHandlers` (audit §4.7, `PushSubscriptionService.cs:85-94`). Tool: NUnit EditMode. Test location: `com.scaffold.directpush/Tests/DispatchExceptionIsolationTests.cs`. Scenario: 3 handlers, middle one throws; assert all three were invoked. Baseline: failing today; trivial to fix with a per-handler try/catch.
- **Send-path serialization cost.** What to measure: ns/op and allocs for `DirectPushClient.SendSelfPushAsync` end-to-end through the LiveOps envelope (`Assets/Packages/com.scaffold.directpush/Runtime/DirectPushClient.cs:18-26`). Tool: Unity.PerformanceTesting. Test location: `com.scaffold.directpush/Tests/SendPathBenchmarks.cs`. Scenario: stub `ILiveOpsService` returning `Task.FromResult(SendPushResponse.Ok())`; 10k iterations. Baseline: dominated by `JObject.FromObject` triple-pass (see CloudCode benchmark plan). Success: serves as upstream regression marker for the CloudCode envelope refactor.
- **Server-auth FixedTimeEquals (correctness).** What to measure: that `GameStateServerAuth.IsValidForServerAccessAsync` rejects mismatched GUIDs without timing leaks. Tool: NUnit + `Stopwatch` over many iterations. Test location: `LiveOps/Tests/LiveOps.Tests/ServerAuthTimingTests.cs`. Scenario: vary mismatch position (first byte vs last byte); assert wall-clock time variance below noise threshold. Baseline: `FixedTimeEquals` is constant-time by contract — the test just pins it so a future "optimization" can't regress.

