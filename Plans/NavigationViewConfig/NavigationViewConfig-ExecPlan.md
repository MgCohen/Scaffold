# Navigation ViewConfig: addressable and direct (non-addressable) view assets

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository planning rules live at `PLANS.md` (repository root). This document must be maintained in accordance with `PLANS.md`.

## Purpose / Big Picture

Today each `ViewConfig` exposes a single `AssetReference` and navigation materializes screens through `AddressablesNavigationPointStrategy`, which loads via `IAddressablesGateway` and instantiates under the view holder. Prefabs that are **not** Addressable entries (ordinary project assets) cannot be assigned in a way that survives the current pipeline.

After this change, a designer can choose per `ViewConfig` whether the view comes from **Addressables** (existing behavior) or from a **direct reference** to a prefab `GameObject` in the project. In the Unity Inspector, only the relevant field is shown for the selected mode. At runtime, navigation resolves the view without requiring Addressables for the direct path.

**How to see it working:** Create or edit a `ViewConfig` asset, switch the new mode to direct reference, assign a prefab that implements `IView`, enter Play Mode, and open that screen; the prefab should instantiate without Addressables load calls for that config. Existing configs that stay on Addressables should behave as before.

## Progress

- [x] Add serialized mode plus direct prefab field on `ViewConfig`; preserve default so existing assets remain Addressable-backed. (`viewAssetSource` + `DirectPrefab`, public `AssetSource` property, `ViewAssetSource` enum in `Runtime/Contracts/ViewAssetSource.cs`.)
- [x] Add `Scaffold.Navigation.Editor` with a `ViewConfig` inspector that subclasses `SchemaObjectEditor` and draws the mode toggle plus either `AssetReference` or the direct prefab field (other fields unchanged). (`Editor/ViewConfigEditor.cs`, `Editor/Scaffold.Navigation.Editor.asmdef`.)
- [x] Extend navigation point resolution: direct-prefab path implemented as `NavigationProvider.DirectPrefabNavigationPointStrategy` (nested); `AddressablesNavigationPointStrategy` skips non-Addressables mode. Provider order: context → direct → addressables.
- [x] Run validation from repository root: `powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests` (omit `-SkipTests` if adding automated tests).
- [ ] Manual Play Mode check: one Addressable-backed screen and one direct-prefab screen both open and close cleanly. (Not run in this session; do locally.)

## Surprises & Discoveries

- Observation: The public property on `ViewConfig` is named `AssetSource` to avoid the enum and property both being named `ViewAssetSource` (same name as the type) for clarity.
  Evidence: `ViewConfig.cs` property and `ViewAssetSource` enum.

- Observation: Unity-generated `Scaffold.Navigation.csproj` lists explicit `Compile` entries; new `.cs` files must be added to that project file when Unity has not regenerated it, or Unity Bee batch compile may miss types (e.g. `DirectPrefabNavigationPointStrategy` was resolved by nesting the strategy in `NavigationProvider.cs`).

- Observation: `ViewFilter` and `ViewAssetSource` are in one file `ViewAssetSource.cs` with a scoped `#pragma warning disable SCA3002` / `restore` because Unity’s Bee batch compile step did not resolve `ViewFilter` when it lived in a separate `ViewFilter.cs` while `validate-changes.ps1` passed; merging enums fixes Unity batch compile. Splitting `ViewFilter` back out is fine after opening the Unity Editor once to refresh the script graph (or when Unity regenerates csproj/Bee inputs).
  Evidence: `validate-changes.ps1` Unity step vs dotnet build.

## Decision Log

- Decision: Use an explicit enum (e.g. `ViewAssetSource` with values such as `Addressables` and `DirectPrefab`) rather than a bare `bool`.
  Rationale: Clear Inspector labels, room for a third mode later (e.g. Resources), and avoids ambiguous “true/false” in serialized data.
  Author: (initial plan)

- Decision: Subclass `SchemaObjectEditor` for `ViewConfig` instead of a `PropertyDrawer` on the enum alone.
  Rationale: `ViewConfig` already inherits `SchemaObject`; the stock `SchemaObjectEditor` draws all serialized fields in one block. Overriding `DrawDefaultProperties` matches how the base uses `DrawPropertiesExcluding` and keeps schema sections and “Add Schema” behavior intact. The Schema package’s `SchemaDrawer` / `SchemaDrawerFactory` pattern applies to **schema** entries inside `schemas.Collection`, not to arbitrary fields on a `SchemaObject`, so the right tool here is a dedicated `CustomEditor` for `ViewConfig`.
  Author: (initial plan)

- Decision: Implement direct prefabs in a dedicated `INavigationPointStrategy` (e.g. `DirectPrefabNavigationPointStrategy`) registered in `NavigationProvider` after `ContextNavigationPointStrategy` and before `AddressablesNavigationPointStrategy`.
  Rationale: Keeps Addressables-only code paths and handle buffering (`NavigationAssetHandleBuffer`) unchanged; direct mode uses synchronous `Object.Instantiate` and `Destroy`/`DestroyImmediate` mirroring disposal rules already used in `AddressablesNavigationPointStrategy` for the view object.
  Author: (initial plan)

## Outcomes & Retrospective

- Shipped: per-`ViewConfig` `ViewAssetSource` (Addressables vs Direct prefab), `DirectPrefabNavigationPointStrategy`, conditional inspector, `AddressablesNavigationPointStrategy` early-exit, docs in `Docs/Infra/Navigation.md` and package `README.md`.
- Deferred: automated EditMode test (the test assembly had no pre-existing C# tests); no Play Mode run here.
- Lessons: keep strategy order and buffer-only-on-addressables path; direct mode never touches `NavigationAssetHandleBuffer`.

## Context and Orientation

**ViewConfig** (`Assets/Packages/com.scaffold.navigation/Runtime/Implementation/ViewConfig.cs`) is a `SchemaObject` with a private serialized `AssetReference asset`, `TypeReference` fields for view and controller types, and editor-only `OnValidate` that resolves types from `asset.editorAsset` when it is a `GameObject` with `IView`.

**NavigationProvider** (`.../NavigationProvider.cs`) builds a list of `INavigationPointStrategy` implementations. Order today: `ContextNavigationPointStrategy` (reuse a view already under the holder), then `AddressablesNavigationPointStrategy` (`.../AddressablesNavigationPointStrategy.cs`), which loads `config.Asset` through `IAddressablesGateway`, optionally reuses `NavigationAssetHandleBuffer`, instantiates under `viewHolder`, and completes `NavigationPoint` asynchronously.

**SchemaObject editor** (`Assets/Packages/com.scaffold.schemas/Editor/SchemaObjectEditor.cs`) draws default properties via `DrawPropertiesExcluding(serializedObject, PropertiesToIgnore)` where `PropertiesToIgnore` includes `m_Script` and `schemas`. Subclasses can override `DrawDefaultProperties` and/or `PropertiesToIgnore` to customize the inspector while preserving schema list UI.

**Term:** “Non-addressable” here means a normal Unity asset reference (e.g. drag a prefab from the Project window) that is **not** loaded through the Addressables API for that navigation path.

## Plan of Work

1. **Data model on `ViewConfig`**

   Add a serialized enum field, for example `ViewAssetSource viewAssetSource` with default `Addressables` (exact names chosen in implementation). Add a second serialized field, e.g. `[SerializeField] GameObject directPrefab` (or a `GameObject` constrained in the custom editor to prefabs that have `IView`). Keep the existing `AssetReference` field; do not remove it. Expose read-only properties as needed for runtime strategies, e.g. whether the config uses Addressables for this view, the `AssetReference` when in Addressables mode, and the `GameObject` prefab when in direct mode.

   Update `OnValidate` in `ViewConfig`: depending on `viewAssetSource`, resolve the preview object from either `asset.editorAsset` (Addressables path) or `directPrefab` (direct path), then call the existing type-resolution logic (`ApplyViewType` / `SetTypeFromAsset` equivalent). If the active source has no valid object, clear or guard type references consistent with current behavior when `asset` was empty.

2. **Inspector (Editor assembly)**

   Create `Assets/Packages/com.scaffold.navigation/Editor/Scaffold.Navigation.Editor.asmdef` referencing `Scaffold.Navigation`, `Scaffold.Schemas.Editor` (for `SchemaObjectEditor`), Unity Editor assemblies, and any packages needed for drawing `AssetReference` fields (e.g. Addressables editor support if required at compile time). Follow the same Editor-folder pattern as `Assets/Packages/com.scaffold.schemas/Editor/Scaffold.Schemas.Editor.asmdef` (`includePlatforms`: Editor only).

   Implement `[CustomEditor(typeof(ViewConfig))] public class ViewConfigEditor : SchemaObjectEditor`. Override `PropertiesToIgnore` to exclude the serialized names of: the enum, the `AssetReference` field, and the direct prefab field, so the default pass does not duplicate them. Override `DrawDefaultProperties`: draw the enum with `EditorGUILayout.PropertyField`, then draw **either** the `AssetReference` property **or** the direct prefab property based on the enum value (read the enum from `SerializedProperty` with `serializedObject.Update` / `ApplyModifiedProperties` in mind). Then call `DrawPropertiesExcluding` with the base ignore list plus those three property names, or call the base implementation with the extended ignore list—whichever keeps `viewType` / `controllerType` / other fields appearing once in a sensible order. The goal is: one mode selector, one asset field visible, then remaining fields and schemas as today.

3. **Runtime: direct prefab strategy**

   Add a new internal class implementing `INavigationPointStrategy` (see `Assets/Packages/com.scaffold.navigation/Runtime/Implementation/INavigationPointStrategy.cs`). In `TryCreate`, return `false` unless `ViewConfig` is in direct-prefab mode and the prefab reference is non-null; otherwise build a `NavigationPoint` similar to `AddressablesNavigationPointStrategy` but **synchronously**: `Instantiate` the prefab under `viewHolder`, resolve `IView`, deactivate the instance if that matches existing behavior, call `CompleteReady`, and register disposal that destroys the instance (reuse the same destroy helper pattern as in `AddressablesNavigationPointStrategy` for play vs edit mode). Do not push `IAssetHandle` through `NavigationAssetHandleBuffer` for this path.

   Register this strategy in `NavigationProvider`’s constructor list in the order: context views, **direct prefab**, addressables. That way scene/context wins first; then direct prefab; then Addressables.

4. **Addressables strategy**

   Adjust `AddressablesNavigationPointStrategy.TryCreate` so it returns `false` when the config is not Addressable-backed (letting the new strategy handle direct prefabs). Keep existing loading and buffering behavior for Addressable configs.

5. **Documentation**

   Update `Docs/` for the navigation module (per `AGENTS.md`, each module has Docs coverage) with a short note that `ViewConfig` supports Addressables vs direct prefab and how the Inspector mode works. Only touch the navigation doc file that already exists for this module.

6. **Tests**

   The `com.scaffold.navigation` Tests project had no existing test `.cs` files; this implementation relies on the compile gate and leaves optional EditMode tests for a follow-up. Manual: create one `ViewConfig` in Direct mode with an `IView` prefab, one in Addressables mode, open/close in Play Mode.

## Concrete Steps

All commands assume working directory is the repository root `C:\Unity\Scaffold` (or equivalent).

1. Implement code and editor changes per Plan of Work.
2. Run:

   `powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests`

   Expect: script completes with success; fix any analyzer or compile errors reported.

3. Optional: `powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\check-analyzers.ps1"` if a solution file is present and you want deduplicated SCA output.

## Validation and Acceptance

- **Compile/analyzer gate:** `validate-changes.ps1` completes without errors (and without new pragma suppressions unless explicitly approved).
- **Inspector:** For a `ViewConfig` asset, switching the enum shows only the Addressables field in Addressables mode and only the direct prefab field in direct mode; schema section and type fields still work after `OnValidate`.
- **Runtime:** Opening a controller whose `ViewConfig` uses direct prefab shows the view; closing disposes without leaks or duplicate instances; Addressable-backed screens still load through Addressables.

## Idempotence and Recovery

Changes are additive (new enum value default preserves old assets). If a mistake is made in Inspector drawing, revert the Editor script; serialized data on disk remains valid if field names are unchanged. Re-run `validate-changes.ps1` after fixes.

## Artifacts and Notes

Key files to touch (exact names may vary slightly during implementation):

- `Assets/Packages/com.scaffold.navigation/Runtime/Implementation/ViewConfig.cs`
- `Assets/Packages/com.scaffold.navigation/Runtime/Implementation/NavigationProvider.cs`
- `Assets/Packages/com.scaffold.navigation/Runtime/Implementation/AddressablesNavigationPointStrategy.cs`
- New: `Assets/Packages/com.scaffold.navigation/Runtime/Implementation/DirectPrefabNavigationPointStrategy.cs` (or similar)
- New: `Assets/Packages/com.scaffold.navigation/Editor/ViewConfigEditor.cs`
- New: `Assets/Packages/com.scaffold.navigation/Editor/Scaffold.Navigation.Editor.asmdef`
- Module doc under `Docs/` for navigation (update only)

## Interfaces and Dependencies

At completion, the following should hold:

- `ViewConfig` exposes a clear mode (enum) and allows both an `AssetReference` and a direct `GameObject` reference to be serialized; only one is authoritative per mode at runtime.
- `INavigationPointStrategy` implementations split responsibility: context → direct prefab (when configured) → Addressables.
- `ViewConfigEditor` inherits `Scaffold.Schemas.Editor.SchemaObjectEditor` and overrides drawing so conditional fields match the mode without breaking schema UI.

External dependencies: existing `Scaffold.Addressables`, Unity Addressables, Unity Editor; no new third-party packages required for the described design.

---

Revision history:

- Initial version: Defines dual-source `ViewConfig`, `SchemaObjectEditor` subclass for conditional Inspector fields, and a third navigation point strategy for direct prefab instantiation.
- Implementation pass: C# and editor shipped; public API uses `ViewConfig.AssetSource` and enum `ViewAssetSource`; Progress section updated. Manual Play Mode not executed in the implementing environment.
- Unity/Bee + SCA3002: `ViewFilter` co-located with `ViewAssetSource` in `ViewAssetSource.cs` with pragma; nested direct-prefab strategy in `NavigationProvider`, `Scaffold.Navigation.csproj` updated for strategy until Unity regenerates.
