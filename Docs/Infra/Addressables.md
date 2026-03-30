# Scaffold Infra Addressables

## TL;DR

- Purpose: keep Addressables runtime small and focused on loading APIs.
- Location: `Assets/Scripts/Assets/Addressables/Runtime/`.
- Depends on: `Scaffold.Scope`, `Unity.Addressables`, `VContainer`, `Scaffold.Maps`. Editor also references `com.unity.services.ccd.management` for CCD **Build & Release** (see `Packages/manifest.json`).
- Used by: bootstrap startup and runtime services that load assets.

## Responsibilities

- Exposes `IAddressablesGateway` for loading by reference/label.
- Performs best-effort catalog/content sync at startup (`InitializeAsync`).
- Owns reference tracking and release policy in `AddressablesAssetReferenceHandler`.
- Provides provider contracts for bootstrap preload and child registration:
  - `IAssetProvider` / `IAssetProvider<TAsset>`
  - `IAssetRegistrar`
  - `AssetProvider<TAsset>` base class

This module no longer owns preload config parsing/build/apply inside the gateway.

## Public API

| Symbol | Purpose |
|---|---|
| `IAddressablesGateway.InitializeAsync` | Best-effort startup sync (no preload pipeline). |
| `IAddressablesGateway.LoadAsync<T>(AssetReference)` | Load one typed asset by reference. |
| `IAddressablesGateway.LoadAsync<T>(AssetReferenceT<T>)` | Typed convenience overload. |
| `IAddressablesGateway.LoadAsync<T>(AssetLabelReference)` | Load typed assets by label. |
| `IAddressablesGateway.Load<T>(...)` | Deferred load variants returning handle/group handle. |
| `IAssetProvider.PreloadAsync` | Provider-local preload entry point. |
| `IAssetRegistrar.Register` | Provider-local typed child registration entry point. |

## Runtime Flow

1. `AddressablesInstaller` registers one scoped `AddressablesGateway` plus required client/handler.
2. Scope startup executes `IAsyncLayerInitializable` on the gateway.
3. Gateway runs best-effort `SyncCatalogAndContentAsync`.
4. Runtime loading uses `Load/LoadAsync` APIs.
5. Bootstrap-level preload/registration is handled outside the gateway by provider/registrar flow.

## Provider and Registrar Flow

- `IAssetProvider` is responsible only for obtaining/storing assets.
- `IAssetRegistrar` is responsible only for writing typed registrations to child builders.
- Concrete classes can implement both interfaces.
- Concrete providers may inject `IAddressablesGateway` in constructor; the interface itself does not require it.

## Remote catalog and Cloud Content Delivery (CCD)

Addressable Asset Settings enable **Build Remote Catalog** and **CCD** (`Assets/AddressableAssetsData/AddressableAssetSettings.asset`). **Remote Catalog Build Path** and **Remote Catalog Load Path** use the same profile variables as remote bundles (**RemoteBuildPath** / **RemoteLoadPath**).

Sample remote group: **Remote Weapons (Sample)** holds `GreatSword`, `CurvedSword`, and `LongSword` (prefabs under `Assets/Prefabs/Weapons/`) with **RemoteBuildPath** and **RemoteLoadPath** so they ship from the remote host, not **Default Local Group**.

**Package:** `com.unity.services.ccd.management` (CCD Management) is listed in `Packages/manifest.json` for Addressables **Build & Release** in the Editor.

**Editor workflow (required for a working CCD URL):**

1. Link the Unity project to your **Unity Cloud** project (**Edit > Project Settings > Services**).
2. In the Unity Dashboard, open **Cloud Content Delivery**, create a **development** bucket (for example `Scaffold Addressables Sample`). Prefer a **public** bucket for first tests; **private** buckets need `Addressables.WebRequestOverride` to add the bucket access token header (see Unity’s CCD + Addressables documentation).
3. Open **Window > Asset Management > Addressables > Groups > Manage Profiles**. Set the **Remote** path source to **Cloud Content Delivery** and select that bucket so **RemoteLoadPath** becomes the HTTPS `client-api.unity3dusercontent.com` entry-by-path base (Unity fills this; do not commit real project or bucket IDs as plain text in shared docs).
4. Run **Build > Build & Release** to upload built content and create a **release** (updates the **`latest`** badge). Alternatively: **New Build > Default Build Script**, then Dashboard **Upload** the **RemoteBuildPath** output and **Create Release**.

Runtime startup still runs `SyncCatalogAndContentAsync` on `IAddressablesGateway` initialization; CCD only changes where catalog and bundle URLs resolve.

## Best Practices

- Keep all Addressables loads through `IAddressablesGateway`.
- Keep provider preload logic module-local and typed.
- Keep registrar logic minimal: only register assets already preloaded.
- Release handles/groups exactly once.

## Testing

- EditMode: `Scaffold.Addressables.Tests`
- PlayMode: `Scaffold.Addressables.PlayModeTests`

Run from repository root:

```powershell
& ".\.agents\scripts\run-editmode-tests.ps1" -AssemblyNames "Scaffold.Addressables.Tests"
& ".\.agents\scripts\run-playmode-tests.ps1" -AssemblyNames "Scaffold.Addressables.PlayModeTests"
```

## Related

- `Architecture.md`
- `Docs/Infra/Scope.md`
- `Assets/Scripts/Assets/Addressables/Runtime/Contracts/IAddressablesGateway.cs`
- `Assets/Scripts/Assets/Addressables/Runtime/Contracts/IAssetProvider.cs`
- `Assets/Scripts/Assets/Addressables/Runtime/Implementation/AddressablesGateway.cs`
- `Assets/Scripts/Assets/Addressables/Runtime/Implementation/AddressablesAssetReferenceHandler.cs`

## Changelog

- Documented CCD remote catalog setup, **Remote Weapons (Sample)** group, and corrected runtime source paths under `Assets/Scripts/Assets/Addressables/`.
- Moved preload ownership out of `AddressablesGateway` to provider/registrar bootstrap flow; removed preload config pipeline files and contracts.
- Updated for gateway-centered simplification and reference-first loading API.
