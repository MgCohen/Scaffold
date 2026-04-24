# LiveOps keys (unified model)

## Goal

A single way to name **where** a DTO is stored in player save, remote config, and snapshot, and how the **GameApi** request is keyed on the wire, without ad-hoc `const` strings, duplicated `Key` members, or `typeof(T).Name` scattered across call sites.

## Declarations (shared DTO)

| Mechanism | Location | Role |
|----------|----------|------|
| `[LiveOpsKey("GoldPersistence")]` | `LiveOps.DTO.Keys.LiveOpsKeyAttribute` (in **`LiveOps.DTO.dll`**) | Optional shared module name or per-type name; **`KeyOf<T>.Module`** reads this. Default migration uses **`nameof`-style** values so existing rows stay under the same key. |
| `[GameApiRequest("Custom.Wire")]` | same assembly | Optional override for the **wire** key (otherwise `KeyOf` uses **`Type.Name`** for `ModuleRequest` types). |
| `IGameModuleData` | `LiveOps.DTO.GameModule` | **Marker only** for types that appear in `GameData.ModulesData`. Persistence and config DTOs do **not** implement it. |
| `KeyOf`, `KeyOf<T>`, `LiveOpsKeyResolver` | `LiveOps.DTO.Keys` | Runtime key resolution; safe for client and server that reference **`LiveOps.DTO`**. |
| `LiveOps.Keys.Generated.LiveOpsKeys` | source-generated when building **Deploy/LiveOps** with **`Scaffold.LiveOps.Bootstrap.Generators`** | `const string` for each **distinct** `[LiveOpsKey]` value (prefetch hints, logging, `switch` on key). |

## Historical key kinds (before unification)

| Kind | Old pattern | Now |
|------|------------|-----|
| Player / remote-config slot | `Get`/`Set` on `IReadableDataCache` / `IWriteableDataCache` with `typeof(T).Name` or `value.Key` | `KeyOf<T>.Module` from `[LiveOpsKey]`. |
| Aggregated `GameData` | `List<IGameModuleData>`; some DTOs wrongly implemented the interface to satisfy cache extension constraints | `IGameModuleData` only on snapshot DTOs; cache uses `[LiveOpsKey]`. |
| `IGameClientModule` / `IGameModule` | `Key` = `typeof(T).Name` | `Key` = `KeyOf<T>.Module`. |
| GameApi / Cloud Code | `RequestKey = request.GetType().Name` | `KeyOf.WireOf(request)` (default matches prior `Type.Name` for request types). |
| Registry | `requestType.Name` | `KeyOf.WireOf(requestType)`. |
| `ModuleRequest.FunctionName` | `GetType().Name` | `KeyOf.WireOf(this)` |

## Analyzers (Scaffold)

| ID | What |
|----|------|
| SCA7101 | Missing `[LiveOpsKey]` on concrete `IGameModuleData` or on types that inherit `ModuleRequest` in LiveOps DTO namespaces. |
| SCA7102 (Info) | String literal for `key` on `IReadableDataCache` / `IWriteableDataCache` where analyzable. |
| SCA7103 (Info) | String literal for `GameApiEnvelopeRequest.RequestKey` (test paths ignored). |

## Build-time generator (Deploy)

- **`LOPSKEY001`**: duplicate **wire** key for `ModuleRequest` subtypes in non-test assemblies (mirrors `GameApiRegistry` runtime check).
- **`LiveOpsKeys.g.cs`**: one `public const` per distinct attribute value in scanned LiveOps-referenced types.

## See also

- [`com.scaffold.liveops/README.md`](../../Assets/Packages/com.scaffold.liveops/README.md) (full pipeline and add-module steps).
- [`LiveOps.md`](LiveOps.md) (short backend index).
