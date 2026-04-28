# Entities Runtime — Editor partial extraction pass

## Goal

Apply the existing `EntityInstance` / `EntityInstance.Editor.cs` pattern across Scaffold.Entities Runtime: `partial` types with sibling **`TypeName.Editor.cs`** files wrapping **`#if UNITY_EDITOR`**, keeping [Scaffold.Entities.asmdef](../Assets/Packages/com.scaffold.entities/Runtime/Scaffold.Entities.asmdef).

## Reference

- Primary: [`EntityInstance.cs`](../Assets/Packages/com.scaffold.entities/Runtime/Core/Instance/EntityInstance.cs) / [`EntityInstance.Editor.cs`](../Assets/Packages/com.scaffold.entities/Runtime/Core/Instance/EntityInstance.Editor.cs)

## Inventory (Runtime `#if UNITY_EDITOR` islands)

| Type | Primary file | Move to `.Editor.cs` |
|------|--------------|----------------------|
| `VariableBag` | VariableBag.cs | `EditorApplyVariableAuthoringFromValidation()` |
| `VariableEntry` | VariableEntry.cs | `variableAuthoring` + `EditorApplyAuthoringIntoInlineSerializedKeyAndClearLegacy()` — keep **`RebaseSerializedPayloadIfMismatch()`** runtime |
| `EntityModifierEntry` | EntityModifierEntry.cs | mirror of VariableEntry authoring block |
| `LocalVariableStorage` | LocalVariableStorage.cs | `NotifyAllEffectiveValues()`, `EditorApplyVariableAuthoringOnBagsFromValidation()` |
| `EntityComponent<TDefinition>` | EntityComponentT.cs | **`OnValidate()`** (whole method) |
| `EntityModifierEntryAsset` | EntityModifierEntryAsset.cs | **`OnValidate()`** (whole method) |
| `EntityDefinitionAsset` | EntityDefinitionAsset.cs | **`OnValidate()`** — **entire method** moves to partial (editor-focused; mirrors other ScriptableObjects in this pass) |

## EntityDefinitionAsset (chosen approach)

**Do not** use a partial-method hook from runtime `OnValidate` into editor-only glue.

Instead:

1. Make `EntityDefinitionAsset` **`partial`**.
2. **Remove** `OnValidate` entirely from the primary file.
3. In **`EntityDefinitionAsset.Editor.cs`**, wrap the file in **`#if UNITY_EDITOR`** and implement the **full** `private void OnValidate()` there, including:
   - `definition.Bag.EditorApplyVariableAuthoringFromValidation();`
   - `RebuildLookup();`

`RebuildLookup()` remains **`internal`** on the merged partial type — callable from the editor partial in the same assembly.

Player builds omit the editor compilation unit; `OnValidate` is editor-only ScriptableObject behavior, same as stripping an inner `#if UNITY_EDITOR` line from the historic source.

## Execution order

1. `VariableEntry.Editor` + `EntityModifierEntry.Editor` (leaf types).
2. `VariableBag.Editor`.
3. `LocalVariableStorage.Editor`.
4. **`EntityDefinitionAsset.Editor`** — full `OnValidate` (depends on VariableBag editor API).
5. `EntityModifierEntryAsset.Editor` — full `OnValidate`.
6. `EntityComponentT.Editor` — full `OnValidate`.

## Assets

Add **`.meta`** for each new `*.Editor.cs` (prefer Unity MCP per AGENTS).

## Verification

Repo root: `pwsh -NoProfile -File ".agents/scripts/validate-changes.ps1" -SkipTests`
