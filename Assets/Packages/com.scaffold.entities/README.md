# com.scaffold.entities

# Entities (Core)

## TL;DR

- Purpose: **Definition** / **instance** flyweight model with **`AttributeSO`** identity, string **`Attribute`** payloads, instance-only **modifiers**, **`InstanceId`**, and **factories**. Optional **behavior runner** (`EntityBehaviorRunner<TData,TInput>`) for per-frame arbitration.
- Location: `Assets/Packages/com.scaffold.entities/Runtime/` — **`Core/`** (definitions, attributes, instances, factory) and **`Behavior/`** (behavior contracts and runner). Single assembly `Scaffold.Entities`; tests in `Assets/Packages/com.scaffold.entities/Tests/` (`Scaffold.Entities.Tests`).
- **Unity coupling:** References `UnityEngine` (`MonoBehaviour`, `ScriptableObject`). `Scaffold.Entities.asmdef` has `noEngineReferences: false`. See [Architecture.md](../../../Architecture.md): the Core folder does not mean “no Unity.”
- Depends on: Unity engine only (no cross-assembly references to other first-party modules in this repository snapshot).
- **Consumers:** Add a reference from `Scaffold.Entities` in your module’s `.asmdef` when you use these types in gameplay or presentation code.

## Folder layout (conceptual split)

| Folder | Responsibility |
|--------|----------------|
| `Runtime/Core/` | `Attribute`, `AttributeSO`, `EntityDefinition`, `EntityInstanceState`, `EntityModifierEntry`, `Entity`, `EntityInstanceFactory`, `InstanceId` — stats, modifiers, instance storage. |
| `Runtime/Behavior/` | `IEntityBehavior`, `IEntityFrameInputProvider`, `EntityBehaviorRunner` — per-frame behavior arbitration. |
| `Samples/` | Optional **Scaffold.Entities.Samples** assembly (`autoReferenced: false`): `SampleEntity` prefab, authored attributes + `SampleCharacterDefinition`, and scripts showing definition → instance, numeric modifier combine, and `EntityBehaviorRunner` with WASD movement. |

## Samples

- Assembly: `Samples/Scaffold.Entities.Samples.asmdef` — reference it from your game assembly if you want to open or extend the sample types.
- Assets: `Samples/Authoring/` — `Attribute_Health`, `Attribute_MoveSpeed`, and `SampleCharacterDefinition` (defaults wired to those slots).
- Prefab: `Samples/SampleEntity` — drop into a scene and press Play: console logs show base stats plus a `+25` health modifier (float combine); on-screen HUD shows effective payloads; **WASD / arrow keys** move on the XZ plane using the effective Move Speed payload.

## Public API (selection)

| Symbol | Role |
|--------|------|
| `Attribute` | Serializable struct: string **payload** and optional **MatchKey** (second-party string matching). |
| `AttributeSO` | `ScriptableObject` slot identity; **implicit** conversion to `Attribute` (payload + asset name as `MatchKey`). |
| `EntityDefinition` | Shared defaults keyed by **`AttributeSO`**; **no** modifiers on definitions. |
| `EntityDefinitionDefaultEntry` | One default row: `AttributeSO` + optional payload override. |
| `EntityInstanceState` | Serializable state: `InstanceId`, `EntityDefinition`, modifier list; `TryGetAttribute` overloads (SO, `Attribute` template, string scan). |
| `EntityInstanceFactory` | `CreateState` and `CreateOnGameObject<TEntity>`. |
| `Entity` | `MonoBehaviour` host delegating to `EntityInstanceState`. |
| `EntityInstance<TDefinition>` | Typed `Definition` accessor without casting. |
| `EntityModifierEntry` | Instance-only modifier: `AttributeSO` + contribution string. |
| `IEntityBehavior<TData,TInput>` | Behavior contract: `TryAcceptControl`, `Execute`, `OnQuit`. |
| `IEntityFrameInputProvider<TInput>` | `GetFrameInput()` for runners that need per-frame context. |
| `EntityBehaviorRunner<TData,TInput>` | Runs behaviors in order; first accepting behavior wins; tracks `OnQuit` when switching flows. |

## Lookup semantics

- **First-party:** resolve by **`AttributeSO`** reference or by **`Attribute`** from an implicit cast (uses **`MatchKey`** = asset name to reach the same slot).
- **Second-party:** `TryGetAttribute(string)` scans slots and matches **MatchKey**, then asset **name**, then **payload** (ordinal).
- **Modifier combination:** if base and contributions all parse as invariant-culture floats, values are **summed**; otherwise strings are **concatenated** in order.

## Testing

- Assembly: `Scaffold.Entities.Tests` (EditMode). Run via `.agents/scripts/run-editmode-tests.ps1` or full `validate-changes.ps1`.
- `EntityInstanceStateTests` covers definition resolution, modifiers (numeric combine), **`RemoveModifierAt` / `ClearModifiers`** restoring base payloads, invalid removal, `CreateOnGameObject`, and non-empty `InstanceId`.

## Related

- `../../../Docs/App/AppStartup.md` (composition; wire consumers when you add gameplay modules that reference `Scaffold.Entities`).
- `../../../Architecture.md` (module boundaries).
- ExecPlan: `Plans/EntitiesExpand/EntitiesExpand-ExecPlan.md`.
