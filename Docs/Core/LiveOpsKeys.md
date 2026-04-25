# LiveOps keys (unified model)

## Goal

A single way to name **where** a DTO is stored in player save, remote config, and snapshot, and how the **GameApi** request is keyed on the wire, without ad-hoc `const` strings, duplicated `Key` members, or `typeof(T).Name` scattered across call sites.

## Declarations (shared DTO)


| Mechanism                                     | Location                                                                                                                                                                        | Role                                                                                                                                                                                                                                                                                                                                                                                                                      |
| --------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `[LiveOpsKey("GoldPersistence")]`             | `LiveOps.DTO.Keys.LiveOpsKeyAttribute` (in `**LiveOps.DTO.dll`**)                                                                                                               | Optional shared module name or per-type name; `**KeyOf<T>.Module**` reads this. Default migration uses `**nameof`-style** values so existing rows stay under the same key.                                                                                                                                                                                                                                                |
| `[GameApiRequest("Custom.Wire")]`             | same assembly                                                                                                                                                                   | Optional override for the **wire** key (otherwise `KeyOf` uses `**Type.Name`** for `ModuleRequest` types).                                                                                                                                                                                                                                                                                                                |
| `IGameModuleData`                             | `LiveOps.DTO.GameModule`                                                                                                                                                        | **Marker only** for types that appear in `GameData.ModulesData`. Persistence and config DTOs do **not** implement it.                                                                                                                                                                                                                                                                                                     |
| `LiveOpsKeyResolution`                        | `LiveOps.DTO.Keys`                                                                                                                                                              | Readonly struct: `**Module`** (storage/cache slot) and optional `**Wire**` (GameApi / `ModuleRequest` only; **null** for persistence/config and other non-wire DTOs).                                                                                                                                                                                                                                                     |
| `KeyOf`, `KeyOf<T>`, `LiveOpsKeyResolver`     | `LiveOps.DTO.Keys`                                                                                                                                                              | Runtime key resolution; safe for client and server that reference `**LiveOps.DTO`**. `**KeyOf<T>.Module**` resolves once per closed generic. `**KeyOf<T>.Wire**` is `**string?**`: set for request/wire types, **null** otherwise. `**KeyOf.WireOf(Type)`** / `**GetWireKey**` throw if there is no wire key. There is **no** reflection fallback: every `**[LiveOpsKey]`** type must appear in the source-generated map. |
| `LiveOps.Keys.Generated.LiveOpsKeys`          | source-generated when building **Deploy/LiveOps** with `**Scaffold.LiveOps.Bootstrap.Generators`**                                                                              | `const string` for each **distinct** `[LiveOpsKey]` **module** value on `**IGameModuleData`** snapshot types and `**ModuleRequest**` subtypes only (not persistence/config slot keys).                                                                                                                                                                                                                                    |
| `LiveOps.Keys.Generated.LiveOpsKeyRuntimeMap` | source-generated in **any** assembly that references the generator and defines types with `[LiveOpsKey]` (today: `**LiveOps.Modules.DTO`**, and any future `**LiveOps.*.DTO**`) | Emits a `**KeyValuePair<RuntimeTypeHandle, LiveOpsKeyResolution>[]**` and a `**[ModuleInitializer]**` that calls `**LiveOpsKeyResolver.Contribute**`. No host or Unity bootstrap call is required.                                                                                                                                                                                                                        |


## Historical key kinds (before unification)


| Kind                                | Old pattern                                                                                                 | Now                                                                            |
| ----------------------------------- | ----------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| Player / remote-config slot         | `Get`/`Set` on `IReadableDataCache` / `IWriteableDataCache` with `typeof(T).Name` or `value.Key`            | `KeyOf<T>.Module` from `[LiveOpsKey]`.                                         |
| Aggregated `GameData`               | `List<IGameModuleData>`; some DTOs wrongly implemented the interface to satisfy cache extension constraints | `IGameModuleData` only on snapshot DTOs; cache uses `[LiveOpsKey]`.            |
| `IGameClientModule` / `IGameModule` | `Key` = `typeof(T).Name`                                                                                    | `Key` = `KeyOf<T>.Module`.                                                     |
| GameApi / Cloud Code                | `RequestKey = request.GetType().Name`                                                                       | `KeyOf.WireOf(request)` (default matches prior `Type.Name` for request types). |
| Registry                            | `requestType.Name`                                                                                          | `KeyOf.WireOf(requestType)`.                                                   |
| `ModuleRequest.FunctionName`        | `GetType().Name`                                                                                            | `KeyOf.WireOf(this)`                                                           |


## Analyzers (Scaffold)


| ID             | What                                                                                                                     |
| -------------- | ------------------------------------------------------------------------------------------------------------------------ |
| SCA7101        | Missing `[LiveOpsKey]` on concrete `IGameModuleData` or on types that inherit `ModuleRequest` in LiveOps DTO namespaces. |
| SCA7102 (Info) | String literal for `key` on `IReadableDataCache` / `IWriteableDataCache` where analyzable.                               |
| SCA7103 (Info) | String literal for `GameApiEnvelopeRequest.RequestKey` (test paths ignored).                                             |


## Build-time generator (Deploy / `LiveOps` assembly)

- `**LOPSKEY001**`: duplicate **wire** key for `ModuleRequest` subtypes in non-test assemblies (mirrors `GameApiRegistry` runtime check).
- `**LOPSKEY002`**: when compiling `**LiveOps**`, warns if a referenced assembly contains `[LiveOpsKey]` types but has no generated `**LiveOpsKeyRuntimeMap**` (missing analyzer reference on that DTO project).
- `**LiveOpsKeys.g.cs**`: one `public const` per distinct `[LiveOpsKey]` **module** value for `**IGameModuleData`** and `**ModuleRequest**` types only.

## Build-time generator (feature DTO assembly)

- `**LiveOpsKeyRuntimeMap.g.cs**`: emitted only when the compiling assembly defines at least one type with `[LiveOpsKey]`. Includes `**ModuleInitializerAttribute**` polyfill for `**netstandard2.1**`. The **LiveOps (Deploy)** manifest is not emitted in DTO projects; the generator is referenced there for the runtime map only.

## See also

- `[com.scaffold.liveops/README.md](../../Assets/Packages/com.scaffold.liveops/README.md)` (full pipeline and add-module steps).
- `[LiveOps.md](LiveOps.md)` (short backend index).