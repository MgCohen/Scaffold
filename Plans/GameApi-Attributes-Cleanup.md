# GameApi attributes cleanup — drop `[UsesGameApi]` and `[GameApiKey]`

**Status:** **Done (Scaffold root).** The attributes and `GameApiKeyResolver` were removed; the wire key is **`typeof(TRequest).Name`**; `GameApiRegistry` is type-keyed; the manifest generator emits `requestType` / `responseType` for handlers; `LiveOpsService` always routes through the GameApi envelope; analyzer **SCA3007** was retired. Downstream (e.g. Gear-Engine) can bump the Scaffold pin and align DTOs.
**Implementation:** [`GameApiRegistry.cs`](../LiveOps/Core/LiveOps.Core/GameApi/GameApiRegistry.cs), [`GameApiSession.cs`](../LiveOps/Core/LiveOps.Core/GameApi/GameApiSession.cs), [`LiveOpsService.cs`](../Assets/Packages/com.scaffold.liveops/Runtime/LiveOpsService.cs), [`LiveOpsManifestGenerator.cs`](../Generators/Scaffold.LiveOps.Bootstrap.Generators/LiveOpsManifestGenerator.cs), [`LiveOpsManifestEntry.cs`](../LiveOps/Core/LiveOps.Core/Initialize/LiveOpsManifestEntry.cs), [`LiveOpsBootstrapper.cs`](../LiveOps/Core/LiveOps.Core/Initialize/LiveOpsBootstrapper.cs). Removed: `UsesGameApiAttribute`, `GameApiKeyAttribute`, `GameApiKeyResolver` under `LiveOps/Core/LiveOps.DTO/GameApi/`.

---

## 1. Problem

Every GameApi request DTO in this project carries two attributes:

```csharp
[UsesGameApi]
[GameApiKey("AddCurrencyRequest")]
public sealed class AddCurrencyRequest : ModuleRequest<AddCurrencyResponse> { ... }
```

Both are vestigial:

- `**[UsesGameApi]**` is a **client-side routing flag** read by `Scaffold.LiveOps`/`LiveOpsService.CallAsync` to decide between the legacy per-endpoint path and the unified GameApi envelope. Per `[Docs/LiveOps/NewApiAndServices.md` §5](../Docs/LiveOps/NewApiAndServices.md): *"Routing: `LiveOpsService` routes to `GameApi` when the request type has `[UsesGameApi]`. Requests without the attribute still use `request.ModuleName` + `request.FunctionName` (legacy path)."*
  - Server-side **never reads it** (`GameApiDispatcher`, `GameApiRegistry`, `GameApiSession` are all attribute-agnostic).
  - The legacy path target no longer exists in this product: only `[GameApiDispatcher](../LiveOps/Deploy/LiveOps/GameApi/GameApiDispatcher.cs)` carries `[CloudCodeFunction]`. A non-`[UsesGameApi]` request would route to a non-existent Cloud Code function.
- `**[GameApiKey("…")]`** carries the wire `RequestKey` string used inside `[GameApiEnvelopeRequest](../LiveOps/Core/LiveOps.DTO/GameApi/GameApiEnvelopeRequest.cs)`.
  - Read on the client by `LiveOpsService` via `[GameApiKeyResolver.GetKey(typeof(TReq))](../LiveOps/Core/LiveOps.DTO/GameApi/GameApiKeyResolver.cs)`.
  - Read on the server by `[GameApiRegistry.RegisterHandlerType](../LiveOps/Core/LiveOps.Core/GameApi/GameApiRegistry.cs)` (registration) and indirectly by `[GameApiDispatcher.Invoke](../LiveOps/Deploy/LiveOps/GameApi/GameApiDispatcher.cs)` (lookup).
  - **Also** read by `[GameApiSession.InvokeAsync<TReq, TRes>](../LiveOps/Core/LiveOps.Core/GameApi/GameApiSession.cs)` for nested handler-to-handler calls — this is a pure `Type → string → Type` roundtrip with no value (the method already has `typeof(TReq)`).

We already own a Roslyn source generator (`[LiveOpsManifestGenerator](../Generators/Scaffold.LiveOps.Bootstrap.Generators/LiveOpsManifestGenerator.cs)`) that walks every `IGameApiHandler<TReq, TRes>` at build time. It can produce a complete compile-time `(handlerType, requestType, responseType)` map, which is everything the registry actually needs.

---

## 2. Decisions

- Wire shape stays a JSON envelope `{ RequestKey, Payload }`. We do **not** drop `RequestKey` from the wire — only the *attribute* that supplies it. Server keeps a `Dictionary<string, Type>` to decode incoming envelopes.
- Default wire key = `**typeof(TReq).Name`** (e.g. `"AddCurrencyRequest"`). Matches today's strings exactly, so **the wire stays byte-compatible with deployed clients**.
  - Trade-off: renaming a request DTO becomes a wire-breaking change. Mitigated by the optional escape hatch below.
- Keep `[GameApiKey("…")]` as an **optional override** for cases where the C# class needs to be renamed without bumping the wire. Default is reflection-on-type-name; the attribute is only consulted when present.
- `[UsesGameApi]` becomes a **deprecated no-op marker** in the Scaffold package (kept for source-compat; emits an `[Obsolete]` warning). Strip it from this repo's DTOs in a follow-up.
- Server-side `GameApiRegistry` becomes `**Type`-keyed** internally; the string lookup is preserved as a thin wire-decoding wrapper.
- Registration is driven by the source-generated manifest (already partly done by `[LiveOpsBootstrapper](../LiveOps/Core/LiveOps.Core/Initialize/LiveOpsBootstrapper.cs)` → `[GameApiRegistry.RegisterHandlerType](../LiveOps/Core/LiveOps.Core/GameApi/GameApiRegistry.cs)`). Generator is extended to capture `TReq`/`TRes` so the registry can be populated **without runtime reflection**.

---

## 3. Where the work lands


| Change                                                                                                                 | Repo                                                                  | Package / project                                                                                       |
| ---------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| Stop reading `[UsesGameApi]` in `LiveOpsService.CallAsync`; always use the GameApi envelope path                       | **Scaffold**                                                          | `com.scaffold.liveops`                                                                                  |
| Mark `UsesGameApiAttribute` `[Obsolete]`                                                                               | **Scaffold (DTO mirror)** + Gear-Engine (`LiveOps/Core/LiveOps.DTO/`) | both                                                                                                    |
| Stop reading `[GameApiKey]` in `LiveOpsService` for the wire key (use `typeof(TReq).Name`, attribute as override)      | **Scaffold**                                                          | `com.scaffold.liveops`                                                                                  |
| `GameApiRegistry` indexed by `Type`; keep `Dictionary<string, Type>` for wire decoding                                 | Gear-Engine                                                           | `LiveOps/Core/LiveOps.Core/`                                                                            |
| Drop `GameApiKeyResolver` from server-side hot paths (use `typeof(TReq)` directly in `GameApiSession.InvokeAsync`)     | Gear-Engine                                                           | `LiveOps/Core/LiveOps.Core/`                                                                            |
| Source generator emits `TReq` / `TRes` per handler entry                                                               | Gear-Engine                                                           | `Generators/Scaffold.LiveOps.Bootstrap.Generators/`                                                     |
| `LiveOpsManifestEntry` carries `RequestType` / `ResponseType`; `LiveOpsBootstrapper` registers without reflection scan | Gear-Engine                                                           | `LiveOps/Core/LiveOps.Core/Initialize/`                                                                 |
| Strip `[UsesGameApi]` and `[GameApiKey]` from all DTOs                                                                 | Gear-Engine                                                           | `LiveOps/Modules/LiveOps.Modules.DTO/**/Request/`                                                       |
| Update docs and tests                                                                                                  | Gear-Engine                                                           | `Docs/LiveOps/`, `Assets/GearEngine/Scripts/App/Bootstrap/Tests/Editor/MetaLayerInitializationTests.cs` |


---

## 4. Staged plan

Each step is independently shippable. Land them in order; never skip a step.

### Step 1 — Server: index `GameApiRegistry` by `Type`

**Repo:** Gear-Engine. **Wire impact:** none. **Risk:** low.

- `[LiveOps/Core/LiveOps.Core/GameApi/GameApiRegistry.cs](../LiveOps/Core/LiveOps.Core/GameApi/GameApiRegistry.cs)`:
  - Internal map becomes `Dictionary<Type, HandlerEntry>` keyed by `RequestType`.
  - Add `bool TryGet(Type requestType, out HandlerEntry?)`.
  - Keep `bool TryGet(string requestKey, out HandlerEntry?)` as a thin wrapper backed by a parallel `Dictionary<string, Type>` populated at registration time (key = whatever `[GameApiKey]` says today; falls back to `requestType.Name` when the attribute is missing).
- `[LiveOps/Core/LiveOps.Core/GameApi/GameApiSession.cs](../LiveOps/Core/LiveOps.Core/GameApi/GameApiSession.cs)`:
  - `InvokeAsync<TReq, TRes>` switches to `_registry.TryGet(typeof(TReq), …)`. Drop the `GameApiKeyResolver.GetKey` call.
- `[LiveOps/Deploy/LiveOps/GameApi/GameApiDispatcher.cs](../LiveOps/Deploy/LiveOps/GameApi/GameApiDispatcher.cs)`:
  - Unchanged behavior; still `_registry.TryGet(request.RequestKey, …)`.

**Tests:** existing server tests cover both nested (`InvokeAsync`) and dispatcher paths. No new tests required.

### Step 2 — Generator: emit `TReq` / `TRes` per handler

**Repo:** Gear-Engine. **Wire impact:** none. **Risk:** low.

- `[LiveOps/Core/LiveOps.Core/Initialize/LiveOpsManifestEntry.cs](../LiveOps/Core/LiveOps.Core/Initialize/LiveOpsManifestEntry.cs)`:
  - Add `Type? RequestType { get; }` and `Type? ResponseType { get; }` (nullable so non-handler entries — `IGameModule` — keep working). Add a constructor overload that takes them.
- `[Generators/Scaffold.LiveOps.Bootstrap.Generators/LiveOpsManifestGenerator.cs](../Generators/Scaffold.LiveOps.Bootstrap.Generators/LiveOpsManifestGenerator.cs)`:
  - Extend `ImplementsGenericHandler` (or add a sibling `TryGetHandlerArgs`) to also surface `i.TypeArguments[0]` and `i.TypeArguments[1]` when a handler is detected.
  - `Entry` struct gains `TReq` / `TRes` symbols.
  - `Emit` writes them into the entry constructor as `typeof(<TReq full>), typeof(<TRes full>)`.
- `[LiveOps/Core/LiveOps.Core/Initialize/LiveOpsBootstrapper.cs](../LiveOps/Core/LiveOps.Core/Initialize/LiveOpsBootstrapper.cs)`:
  - When `IsGameApiHandler && entry.RequestType != null && entry.ResponseType != null`, call a new `GameApiRegistry.Register(handlerType, requestType, responseType)` overload that **doesn't** read attributes.
  - Keep the existing reflection path as a fallback for entries emitted by older generator versions.

**Tests:** add an editor test that asserts the generated `LiveOpsManifest.g.cs` contains all expected `(request, response, handler)` triples for every request DTO in `LiveOps.Modules.DTO`.

### Step 3 — Server: strip the `[GameApiKey]` requirement

**Repo:** Gear-Engine. **Wire impact:** none (default key still `typeof(TReq).Name`, which matches every existing `[GameApiKey("…")]` value). **Risk:** low.

- `[GameApiRegistry.RegisterHandlerType(Type)](../LiveOps/Core/LiveOps.Core/GameApi/GameApiRegistry.cs)` (the reflection fallback) stops throwing when `[GameApiKey]` is missing; it derives the wire key as `attr?.Key ?? requestType.Name`.
- The `Register(handlerType, requestType, responseType)` overload added in Step 2 does the same (`requestType.Name` as default; honors `[GameApiKey]` when present).
- Audit and remove server reads of `GameApiKeyResolver` outside the dispatcher's wire-decoding helper. Today only `GameApiSession.InvokeAsync` reads it (already handled in Step 1).

**Migration check:** assert at startup that no two registered request types collide on `typeof(T).Name`. If they do, the offending DTO must keep a `[GameApiKey("…")]` override.

### Step 4 — Scaffold: stop gating on `[UsesGameApi]` in `LiveOpsService`

**Repo:** Scaffold (`com.scaffold.liveops`). **Wire impact:** none for this product. **Risk:** medium for other Scaffold consumers.

- `LiveOpsService.CallAsync` always builds a `GameApiEnvelopeRequest` and calls `cloudCodeService.CallEndpointAsync<GameApiEnvelopeResponse>(module, "GameApi", envelope, ct)`. Drop the legacy `request.ModuleName` + `request.FunctionName` branch.
- `LiveOpsService` derives the wire key as `request.GetType().GetCustomAttribute<GameApiKeyAttribute>(inherit: true)?.Key ?? request.GetType().Name`. Consumers can opt into an override but no attribute is required.
- Mark `UsesGameApiAttribute` `[Obsolete("All requests are routed through GameApi; this attribute is a no-op and will be removed.")]` in the DTO assembly.
- Update `[Scaffold.LiveOps` README]([https://github.com/MgCohen/Scaffold/blob/main/Assets/Packages/com.scaffold.liveops/README.md](https://github.com/MgCohen/Scaffold/blob/main/Assets/Packages/com.scaffold.liveops/README.md)) to drop `[UsesGameApi]` from the "how to add a request" steps.

**Migration for other Scaffold consumers:** any project that still relied on the legacy per-function path must move those handlers to `IGameApiHandler<,>` (or pin to a previous Scaffold version). The legacy path was already inactive in `Gear-Engine` because no `[CloudCodeFunction]` is registered for those names; other consumers may differ.

### Step 5 — Gear-Engine: strip the attributes from all DTOs

**Repo:** Gear-Engine. **Depends on Steps 1–4 being released**. **Wire impact:** none. **Risk:** low.

Delete `[UsesGameApi]` and `[GameApiKey("…")]` from:

- `[AddCurrencyRequest.cs](../LiveOps/Modules/LiveOps.Modules.DTO/Currency/Request/AddCurrencyRequest.cs)`
- `[SpendCurrencyRequest.cs](../LiveOps/Modules/LiveOps.Modules.DTO/Currency/Request/SpendCurrencyRequest.cs)`
- `[SetInventoryRequest.cs](../LiveOps/Modules/LiveOps.Modules.DTO/Inventory/Request/SetInventoryRequest.cs)`
- `[SaveBoardLayoutRequest.cs](../LiveOps/Modules/LiveOps.Modules.DTO/Loadout/Request/SaveBoardLayoutRequest.cs)`
- `[ClearBoardRequest.cs](../LiveOps/Modules/LiveOps.Modules.DTO/Loadout/Request/ClearBoardRequest.cs)`
- `[RecordRaceResultRequest.cs](../LiveOps/Modules/LiveOps.Modules.DTO/Tracks/Request/RecordRaceResultRequest.cs)`
- `[DrawRoguelikeRollRequest.cs](../LiveOps/Modules/LiveOps.Modules.DTO/Roguelike/Request/DrawRoguelikeRollRequest.cs)`
- `[ClaimRoguelikePickRequest.cs](../LiveOps/Modules/LiveOps.Modules.DTO/Roguelike/Request/ClaimRoguelikePickRequest.cs)`
- `[PurchaseCardRequest.cs](../LiveOps/Modules/LiveOps.Modules.DTO/Cards/Request/PurchaseCardRequest.cs)`
- `[GameDataRequest.cs](../LiveOps/Modules/LiveOps.Modules.DTO/GameData/GameDataRequest.cs)`
- `MetaOptimisticRequest` in `[MetaLayerInitializationTests.cs](../Assets/GearEngine/Scripts/App/Bootstrap/Tests/Editor/MetaLayerInitializationTests.cs)`.

Optionally delete the now-unused source files in `LiveOps/Core/LiveOps.DTO/GameApi/` (see Step 7 below — only after the Scaffold side has caught up):

- `[UsesGameApiAttribute.cs](../LiveOps/Core/LiveOps.DTO/GameApi/UsesGameApiAttribute.cs)`
- `[GameApiKeyAttribute.cs](../LiveOps/Core/LiveOps.DTO/GameApi/GameApiKeyAttribute.cs)` — *only if* we decide not to keep the optional override
- `[GameApiKeyResolver.cs](../LiveOps/Core/LiveOps.DTO/GameApi/GameApiKeyResolver.cs)` — *only if* nothing else references it

### Step 6 — Update docs and "how to add a request" guides

**Repo:** Gear-Engine. **Wire impact:** none.

Remove the attribute-related steps from:

- `[Docs/LiveOps/GameApi.md](../Docs/LiveOps/GameApi.md)` (lines 7, 21).
- `[Docs/LiveOps/NewApiAndServices.md](../Docs/LiveOps/NewApiAndServices.md)` (request-DTO step in §2 table; §4 Step A code sample; §5 routing paragraph; §A1 cheat-sheet).
- Per-feature docs that mention `[UsesGameApi]`: `[Cards.md](../Docs/LiveOps/Cards.md)`, `[Inventory.md](../Docs/LiveOps/Inventory.md)`, `[Loadout.md](../Docs/LiveOps/Loadout.md)`, `[Roguelike.md](../Docs/LiveOps/Roguelike.md)`, `[Tracks.md](../Docs/LiveOps/Tracks.md)`.
- `[Plans/CloudCode-Optimistic-Returns.md](./CloudCode-Optimistic-Returns.md)` — the "GameApi envelope calls (`[UsesGameApi]` → ...)" header (now: "GameApi envelope calls" only).

### Step 7 — (Optional) Drop `UsesGameApiAttribute` and `GameApiKeyAttribute` entirely

**Repos:** Scaffold + Gear-Engine. **Wire impact:** none. **Risk:** medium (source-compat).

Only do this once every consumer of the Scaffold package has been migrated off the attributes. Until then, keep them as `[Obsolete]` no-ops to preserve compile compatibility.

---

## 5. Risks and trade-offs

- **Renaming a request DTO becomes a wire-breaking change.** Today `[GameApiKey("AddCurrencyRequest")]` shields the wire from C# class renames. Mitigation: keep `GameApiKeyAttribute` as an opt-in override (Step 3 — registry honors it when present).
- **Source-generator drift.** Any handler defined in an assembly the generator doesn't visit will silently miss the manifest. The generator's `IsRelevantRef` filter currently includes `LiveOps.Modules`, `LiveOps.Core`, `LiveOps`, `LiveOps.DTO`, `LiveOps.Modules.DTO` — anything outside that list needs to be added to the allow-list. Keep the existing reflection path in `LiveOpsBootstrapper` as a safety net for at least one release.
- **Other Scaffold consumers.** `com.scaffold.liveops` is consumed by external projects via git-pinned UPM. Removing the legacy per-function path is breaking for anyone still using it. Communicate via a Scaffold release note and a major-version bump.
- `**Newtonsoft.Json` polymorphism.** `GameApiEnvelopeResponse` already uses `TypeNameHandling.Auto` for `Result` and `NestedResponses`. We are not changing the request envelope to use type-name handling — we keep an explicit `RequestKey` string discriminator so the wire stays simple and human-debuggable.
- **Cross-assembly type identity.** DTOs are shared via `Scaffold.LiveOps.DTO.dll`, copied to `Assets/Plugins/Scaffold.LiveOps.DTO/` (per `[NewApiAndServices.md` §4 Step A](../Docs/LiveOps/NewApiAndServices.md)). Both client and Cloud Code reference the same DLL, so `typeof(TReq) == typeof(TReq)` across the boundary. `typeof(TReq).Name` is the safe default.

---

## 6. Validation gates

For each step before merge:

- `LiveOps.Core` build green; no analyzer warnings.
- All editor tests under `Assets/GearEngine/Scripts/**/Tests/Editor/` pass — particularly `MetaLayerInitializationTests`, `CurrencyClientModuleTests`, `RoguelikeClientModuleTests`, `InventoryClientModuleTests`, `CardsClientModuleTests`.
- Cloud Code dotnet build of `LiveOps/Project` (and the deploy DLL) succeeds.
- Manual smoke: meta scene boots, runs `GameDataRequest`, performs an `AddCurrencyRequest` and a `SaveBoardLayoutRequest` end-to-end with a deployed Cloud Code function.
- Diff `GameApiEnvelopeRequest.RequestKey` strings before/after each step — they must remain identical for every existing DTO.

---

## 7. Out of scope

- Replacing the JSON envelope wire format (e.g. moving to `TypeNameHandling.Auto` on the request payload, or to a binary discriminator). Tracked separately if desired.
- Changes to `IGameModule` registration — only `IGameApiHandler<,>` is touched.
- Changes to `CloudCodeOptimisticHandlerRegistry` — already type-keyed by `(TRequest, TResponse)` and unaffected.
- Removing `ModuleRequest.ModuleName` / `ModuleRequest.FunctionName`. Once Step 4 ships, both are unused at runtime, but kept for now to preserve binary compatibility in `Scaffold.LiveOps.DTO.dll`.

---

## 8. Suggested PR breakdown

1. **Gear-Engine PR:** Steps 1 + 2 + 3 (server-side `Type`-keyed registry, generator extension, attribute-optional registration). No DTO changes, no wire change.
2. **Scaffold PR:** Step 4 (drop legacy routing branch + `[Obsolete]` mark on `UsesGameApiAttribute`). Coordinated Scaffold release.
3. **Gear-Engine PR:** Bump Scaffold pin in `[Packages/manifest.json](../Packages/manifest.json)`. Land Steps 5 + 6 (strip attributes + docs).
4. **Scaffold PR (later):** Step 7 (delete the attributes outright in a major version), once external consumers have migrated.

---

## 9. References

- `[Docs/LiveOps/GameApi.md](../Docs/LiveOps/GameApi.md)` — current contract.
- `[Docs/LiveOps/NewApiAndServices.md](../Docs/LiveOps/NewApiAndServices.md)` — current "how to add" steps; §5 line 185 documents the legacy routing branch this plan removes.
- `[Plans/CloudCode-Optimistic-Returns.md](./CloudCode-Optimistic-Returns.md)` — touches `LiveOpsService` GameApi envelope path (unaffected).
- `[Plans/Scaffold-Upstream-LayeredScope.md](./Scaffold-Upstream-LayeredScope.md)` — pattern for landing changes upstream then bumping the pin here.