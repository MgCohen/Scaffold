# Entities API Cleanup: drawer fix, Attribute-first runtime API, self-contained value combine

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository policy for ExecPlans is defined in `PLANS.md` at the repository root. This document must be maintained in accordance with that file.


## Purpose / Big Picture

Three related quality problems remain after the first cleanup pass (see `Plans/EntitiesCleanup/EntitiesCleanup-ExecPlan.md`, now complete):

1. The inspector property drawer for `AttributeEntry` is broken: it allocates only one line of height, so the `[SerializeReference]` value field is clipped to nothing or shows an invisible type-picker. Designers cannot assign or see the base value of any attribute in a definition asset. Additionally, when an `AttributeSO` of type Float is dragged onto an entry, the `baseValue` field stays null — the inspector does not automatically create a matching `FloatAttributeValue` instance.

2. The public runtime API (`IEntity<T>`, `EntityInstance<T>`) accepts `AttributeSO` (a Unity asset reference) as a parameter on every getter and modifier call. `AttributeSO` is an authoring concept (an asset on disk) and should not appear in runtime logic. A plain `Attribute` record (a lightweight struct holding name and type) already exists and is the correct runtime key. The implicit conversion `AttributeSO → Attribute` already exists on `AttributeSO`, so call sites that hold an `AttributeSO` reference will still compile with no cast needed after the API migration.

3. The combine logic (how base value + modifier list = effective value) is spread across `AttributeResolver` (a separate internal class with a long switch statement) and a disconnected `AttributeCombine` helper. Moving combine responsibility onto each `AttributeValue` subtype makes the system self-contained: `FloatAttributeValue` knows how to add float contributions, `StringAttributeValue` knows how to concatenate. A thin `AttributeModifierHandler` replaces `AttributeResolver` as the orchestrator — it stores the modifier list, triggers recalculation, and calls the value type's own combine method. `AttributeCombine` is then deleted.

After this work, designers can see and edit base values in definition assets directly from the inspector. Gameplay code passes `Attribute` keys (not asset references) to entity APIs. The modifier combine path is readable in one place per value type.

You can verify success by: (1) opening any `EntityDefinition` asset in Unity and confirming each entry shows the SO reference on the first row and the value fields (e.g. `value`, `min`, `max` for float) on the second row — assigning an SO auto-creates a matching value; (2) confirming all 96 EditMode tests pass after the API migration (call sites use implicit conversion silently); (3) confirming `AttributeResolver.cs` and `AttributeCombine.cs` are deleted and the project still compiles and tests pass.


## Progress

- [x] Author initial ExecPlan at `Plans/EntitiesApiCleanup/EntitiesApiCleanup-ExecPlan.md` (this file).
- [x] Milestone 1 — Drawer fix + auto-assign: `AttributePropertyDrawer` uses dynamic two-row height and layout; `EntityDefinition.OnValidate` auto-assigns a typed `AttributeValue` when SO type mismatches or value is null. Verify visually in editor.
- [x] Milestone 2 — `Attribute`-first runtime API: `IEntity<T>` and `EntityInstance<T>` take `Attribute` not `AttributeSO`; `EntityModifierEntry` gains a runtime constructor accepting `Attribute`; implicit conversion keeps all existing call sites compiling. All 96 tests pass.
- [x] Milestone 3 — Self-contained combine + `AttributeModifierHandler`: abstract `Combine` on `AttributeValue`; concrete implementations per subtype; `AttributeModifierHandler` replaces `AttributeResolver`; `AttributeCombine` deleted. All 96 tests pass.


## Surprises & Discoveries

- `AttributeSO` implicit conversion returns `new Attribute(string.Empty)` for a null SO; `EntityModifierEntry.AttributeKey` relies on non-null **`AttributeSO`** in serialized entries or the runtime **`Attribute`** constructor.


## Decision Log

- Decision: Keep `AttributeModifierEntry`'s serialized field as `AttributeSO` (not `Attribute`).
  Rationale: Modifier entries created in the inspector (on a prefab or component) must reference an asset so they survive Unity serialization and scene round-trips. The runtime path converts via the implicit operator. Purely runtime modifier entries use the new `Attribute`-keyed constructor.
  Author: Planning session, 2026-04-07.

- Decision: Auto-assign in `EntityDefinition.OnValidate`, not inside the drawer.
  Rationale: `OnValidate` runs reliably on every asset save, script reload, and undo. Placing it in the drawer would only fire on drag, missing keyboard edits and undo. `EntityDefinition` already calls `RebuildLookup()` in `OnValidate`, so extending it is consistent.
  Author: Planning session, 2026-04-07.

- Decision: `AttributeEntry` gets an `internal void EnsureValueMatchesType()` method that `EntityDefinition.OnValidate` calls per entry; the method is also callable from tests.
  Rationale: Keeping the assignment logic on the entry class, rather than duplicating it in both the definition and any future editor script, follows the single-responsibility rule.
  Author: Planning session, 2026-04-07.

- Decision: `AttributeValue.Combine` is abstract with signature `public abstract AttributeValue Combine(IReadOnlyList<AttributeValue> contributions)`.
  Rationale: Placing the combine rule on the value type makes each type self-documenting. The signature is the same for all types so the caller (`AttributeModifierHandler`) needs no type switch.
  Author: Planning session, 2026-04-07.

- Decision: `AttributeModifierHandler` is an `internal sealed class` with the same responsibility as `AttributeResolver` (hold modifiers, expose `GetEffective`) but delegates combine to the value type.
  Rationale: Keeps the modifier orchestration internal; the public surface of `EntityInstance` does not change.
  Author: Planning session, 2026-04-07.


## Outcomes & Retrospective

- Implemented **`AttributeEntry.EnsureValueMatchesType`**, two-row **`AttributePropertyDrawer`**, **`Attribute`**-first **`IEntity`** / **`EntityInstance`**, **`EntityModifierEntry.AttributeKey`**, per-type **`AttributeValue.Combine`**, and **`AttributeModifierHandler`** with on-demand effective values (no resolved cache). Removed **`AttributeResolver`** and **`AttributeCombine`**. Package **`README.md`** updated for lookup and combine semantics. Full **`validate-changes.ps1`** (including EditMode tests) should be run in the repo to confirm the 96-test gate.


## Context and Orientation

All changes are in one package: `Assets/Packages/com.scaffold.entities/`. The relevant sub-folders are:

    Runtime/Core/   — domain types: AttributeValue hierarchy, AttributeEntry, EntityDefinition,
                      EntityInstance, IEntity, EntityModifierEntry, AttributeResolver, etc.
    Editor/         — AttributePropertyDrawer.cs (the broken drawer)
    Tests/          — EntityInstanceTests.cs
    Samples/        — sample scene scripts that use the entity API

### Key terms

An **`AttributeSO`** (Unity ScriptableObject) is an asset that acts as a named, typed slot identifier. It carries an `AttributeValueType` dropdown (Float, Int, Bool, String). It lives at `Runtime/Core/AttributeSO.cs`.

An **`Attribute` record** (`Runtime/Core/Attribute.cs`) is a plain C# record class (`record Attribute(string Key, AttributeValueType Type)`). It is the runtime key used in dictionaries. It has structural equality. `AttributeSO` has an implicit operator that converts it to `Attribute` using `so.name` and `so.ValueType`.

An **`AttributeValue`** (`Runtime/Core/AttributeValue.cs`) is an abstract `[Serializable]` class. The four concrete subtypes (`FloatAttributeValue`, `IntAttributeValue`, `BoolAttributeValue`, `StringAttributeValue`) are each in their own `.cs` file under `Runtime/Core/`. Float and Int carry `Value`, `Min`, `Max`. Bool carries `Value`. String carries `Value`.

An **`AttributeEntry`** (`Runtime/Core/AttributeEntry.cs`) is a `[Serializable]` class with two fields: `[SerializeField] private AttributeSO attribute` and `[SerializeReference] private AttributeValue baseValue`. It represents one row in an `EntityDefinition` asset.

An **`EntityDefinition`** (`Runtime/Core/EntityDefinition.cs`) is a ScriptableObject that owns a `List<AttributeEntry>`. Its `OnValidate` already calls `RebuildLookup()`. After Milestone 1 it also calls `entry.EnsureValueMatchesType()` for each entry.

An **`EntityInstance<TDefinition>`** (`Runtime/Core/EntityInstance.cs`) is a `[Serializable]` C# class (not a MonoBehaviour) that holds the definition, a unique `InstanceId`, a `List<EntityModifierEntry>` modifier list, and an `AttributeResolver` (currently). It implements `IEntity<TDefinition>`. After Milestone 2 its public methods accept `Attribute`, not `AttributeSO`.

An **`AttributeResolver`** (`Runtime/Core/AttributeResolver.cs`) is the internal class that currently holds combine logic in a switch statement. It will be replaced by `AttributeModifierHandler` in Milestone 3.

An **`[SerializeReference]` field** is a Unity mechanism that stores a C# object reference polymorphically. Unity shows a type-selector dropdown and child fields in the inspector. It requires the reported height to account for all child fields, not just one line — this is the root cause of the broken drawer.


## Plan of Work

### Milestone 1 — Drawer fix and auto-assign of `AttributeValue` on SO change

**Goal.** Designers can see and edit attribute base values in the inspector. When an `AttributeSO` is assigned to an entry, a correctly-typed `AttributeValue` instance is automatically created if none exists or if the current instance's type does not match.

**Step 1.1 — Add `EnsureValueMatchesType()` to `AttributeEntry`.**

In `Assets/Packages/com.scaffold.entities/Runtime/Core/AttributeEntry.cs`, add an `internal` method that checks the current `baseValue` against the assigned `attribute.ValueType` and replaces it with a fresh default instance when they do not match or `baseValue` is null. The method creates instances with `new` (not ScriptableObject.CreateInstance) since `AttributeValue` subtypes are plain `[Serializable]` C# classes.

    internal void EnsureValueMatchesType()
    {
        if (attribute == null) return;
        AttributeValueType required = attribute.ValueType;
        if (baseValue != null && baseValue.Type == required) return;
        baseValue = required switch
        {
            AttributeValueType.Float  => new FloatAttributeValue(),
            AttributeValueType.Int    => new IntAttributeValue(),
            AttributeValueType.Bool   => new BoolAttributeValue(),
            AttributeValueType.String => new StringAttributeValue(),
            _                         => null
        };
    }

**Step 1.2 — Call `EnsureValueMatchesType()` from `EntityDefinition.OnValidate`.**

In `Assets/Packages/com.scaffold.entities/Runtime/Core/EntityDefinition.cs`, extend the existing `OnValidate` method to loop over `entries` and call `entry.EnsureValueMatchesType()` before `RebuildLookup()`:

    private void OnValidate()
    {
        for (int i = 0; i < entries.Count; i++)
            entries[i]?.EnsureValueMatchesType();
        RebuildLookup();
    }

**Step 1.3 — Fix `AttributePropertyDrawer`.**

In `Assets/Packages/com.scaffold.entities/Editor/AttributePropertyDrawer.cs`, replace the fixed-height single-row layout with a two-row layout:

- Row 1 (full width): the `attribute` (SO) reference field.
- Row 2 (full width): the `baseValue` managed-reference field, allowing Unity to render type-picker and child fields at whatever height it needs.

`GetPropertyHeight` must sum both rows:

    float soHeight = EditorGUIUtility.singleLineHeight;
    float valHeight = EditorGUI.GetPropertyHeight(valueProp, GUIContent.none, true);
    return soHeight + EditorGUIUtility.standardVerticalSpacing + valHeight;

`OnGUI` places them vertically:

    Rect soRect  = new Rect(position.x, position.y, position.width, singleLineHeight);
    float valY   = position.y + singleLineHeight + standardVerticalSpacing;
    float valH   = EditorGUI.GetPropertyHeight(valueProp, GUIContent.none, true);
    Rect valRect = new Rect(position.x, valY, position.width, valH);
    EditorGUI.PropertyField(soRect, attributeProp, GUIContent.none);
    EditorGUI.PropertyField(valRect, valueProp, new GUIContent("Value"), true);

Note: Unity's `PropertyField` with `includeChildren: true` (the final boolean) is needed for managed references to expand correctly.

**Validation for Milestone 1.** Open the Unity editor. Open any `EntityDefinition` asset (for example `Assets/Packages/com.scaffold.entities/Samples/Authoring/SampleCharacterDefinition.asset`). Each entry should show the SO field on a first row and the value fields on a second, expandable row. Drag a Float-type `AttributeSO` onto an empty entry; the `baseValue` should auto-populate with a `FloatAttributeValue` showing `value`, `min`, `max` fields. Run `validate-changes.ps1 -SkipTests` and confirm exit code 0.


### Milestone 2 — `Attribute`-first runtime API

**Goal.** `IEntity<T>` and `EntityInstance<T>` accept `Attribute` keys, not `AttributeSO` asset references. Existing call sites (tests, samples) that pass an `AttributeSO` variable continue to compile because `AttributeSO → Attribute` is an implicit conversion.

**Step 2.1 — Update `IEntity<T>` signatures.**

In `Assets/Packages/com.scaffold.entities/Runtime/Core/IEntity.cs`, change the parameter type of all attribute-access methods from `AttributeSO attribute` to `Attribute attribute`:

    AttributeValue GetAttribute(Attribute attribute);
    T GetValue<T>(Attribute attribute);
    TAttr GetTypedAttribute<TAttr>(Attribute attribute) where TAttr : AttributeValue;
    bool TryGetAttribute(Attribute attribute, out AttributeValue value);

Do not add modifier-mutation methods to the interface at this time; they remain on `EntityInstance` directly.

**Step 2.2 — Update `EntityInstance<T>` signatures.**

In `Assets/Packages/com.scaffold.entities/Runtime/Core/EntityInstance.cs`, change the parameter types of `GetAttribute`, `GetValue<T>`, `GetTypedAttribute<TAttr>`, and `TryGetAttribute` to accept `Attribute attribute`. Remove the `(Attribute)attribute` casts that were previously needed since the parameter is now already `Attribute`.

**Step 2.3 — Update `EntityBehaviour<TDefinition>` delegation.**

In `Assets/Packages/com.scaffold.entities/Runtime/Core/EntityBehaviourT.cs`, change the forwarded method signatures to match — they delegate to `EnsureInstance().*` so the change is one line per method (parameter rename only).

**Step 2.4 — Add an `Attribute`-keyed constructor to `EntityModifierEntry`.**

In `Assets/Packages/com.scaffold.entities/Runtime/Core/EntityModifierEntry.cs`, add a second constructor that accepts `Attribute key` and `AttributeValue modifierValue`. Store the key in a new `private Attribute attributeKey` field. Adjust the `Attribute` property to prefer `attributeKey` when set:

    private Attribute? attributeKey;

    public EntityModifierEntry(Attribute key, AttributeValue modifierValue)
    {
        this.attributeKey = key;
        this.modifierValue = modifierValue;
    }

    // existing AttributeSO-based constructor stays for inspector/serialization use

    public Attribute AttributeKey =>
        attributeKey ?? (Attribute)attribute;

The resolver/handler uses `entry.AttributeKey` instead of `(Attribute)entry.Attribute` in Milestone 3. For now, update `AttributeResolver.CollectModifiers` to use `entry.AttributeKey` (one line change).

**Validation for Milestone 2.** The compiler must emit zero errors. Run `validate-changes.ps1` (with tests) and confirm all 96 EditMode tests pass. The implicit conversion is verified by the fact that `EntityInstanceTests.cs` still passes `AttributeSO` variables to `TryGetAttribute`, `GetValue`, and `AddModifier` without any cast.


### Milestone 3 — Self-contained combine; `AttributeModifierHandler` replaces `AttributeResolver`

**Goal.** Each `AttributeValue` subtype owns its own combine rule. A new `AttributeModifierHandler` orchestrates the modifier list and calls the value type. `AttributeResolver` and `AttributeCombine` are deleted.

**Step 3.1 — Add abstract `Combine` to `AttributeValue`.**

In `Assets/Packages/com.scaffold.entities/Runtime/Core/AttributeValue.cs`, add one abstract method:

    public abstract AttributeValue Combine(IReadOnlyList<AttributeValue> contributions);

This requires `using System.Collections.Generic;` in the file.

**Step 3.2 — Implement `Combine` on each concrete subtype.**

In `FloatAttributeValue.cs`: sum `contributions` that are `FloatAttributeValue`, clamp to `[Min, Max]`, return a new `FloatAttributeValue` with the result, keeping `Min` and `Max` from `this`.

In `IntAttributeValue.cs`: same pattern using `int` sum and `Math.Clamp`.

In `BoolAttributeValue.cs`: last-write-wins — if any `BoolAttributeValue` contribution exists, its `Value` replaces; return new `BoolAttributeValue`.

In `StringAttributeValue.cs`: concatenate all `StringAttributeValue` contributions after the base; return new `StringAttributeValue`. This replaces the `AttributeCombine.Combine` logic inline, without the old float-parsing fallback (that was a legacy behavior; string attributes are now purely string and float/int have their own types).

**Step 3.3 — Create `AttributeModifierHandler`.**

Create `Assets/Packages/com.scaffold.entities/Runtime/Core/AttributeModifierHandler.cs` (via Unity MCP for a valid meta file). Its contract:

    internal sealed class AttributeModifierHandler
    {
        internal AttributeModifierHandler();                              // empty list
        internal void AddModifier(EntityModifierEntry entry);
        internal bool RemoveModifierAt(int index);
        internal void ClearModifiers();
        internal int Count { get; }

        // Returns base if no matching modifier exists.
        // Returns baseValue.Combine(contributions) otherwise.
        internal AttributeValue GetEffective(Attribute key, AttributeValue baseValue);

        // Exposes the list for serialization by EntityInstance.
        internal List<EntityModifierEntry> Modifiers { get; }
    }

`GetEffective` collects all modifier entries whose `AttributeKey == key`, builds a `List<AttributeValue>` from their `ModifierValue` properties, and calls `baseValue.Combine(contributions)`. If contributions is empty, returns `baseValue` unchanged.

**Step 3.4 — Replace `AttributeResolver` usage in `EntityInstance<T>`.**

In `Assets/Packages/com.scaffold.entities/Runtime/Core/EntityInstance.cs`:

- Replace the `private AttributeResolver resolver;` field with `private AttributeModifierHandler modifierHandler;`.
- Replace the `[SerializeField] private List<EntityModifierEntry> modifiers` field: the handler owns the list, so `modifiers` becomes `modifierHandler.Modifiers` — keep a direct ref or let the handler expose it. Keep the serialized list field but synchronize on `Initialize` (assign handler a new list equal to the serialized list after deserialization). The `EnsureResolver` → `EnsureHandler` idiom: if handler is null, create one and populate from the serialized modifiers list.
- `TryGetAttribute` no longer calls into the handler's resolved cache; instead: get the base from `definition.TryGetBaseValue(attribute, out AttributeValue base)`, then return `modifierHandler.GetEffective(attribute, base)`. This means there is no `Recalculate` step and no separate resolved dictionary. The effective value is computed on each `TryGetAttribute` call. For game-use frequency this is acceptable; the dictionary cache can be re-introduced later if profiling shows it is needed.
- `AddModifier`, `RemoveModifierAt`, `ClearModifiers` delegate to `modifierHandler` (they previously also called `resolver.Recalculate` — that call is removed since there is no cache).
- `Initialize` creates a new `AttributeModifierHandler()` (replaces `new AttributeResolver`).

**Step 3.5 — Delete `AttributeResolver.cs` and `AttributeCombine.cs`.**

Delete both files and their `.meta` files. Also remove their `<Compile Include=...>` lines from `Scaffold.Entities.csproj`.

**Validation for Milestone 3.** Run `validate-changes.ps1` (with tests). All 96 tests must pass. Confirm `AttributeResolver.cs` and `AttributeCombine.cs` are gone. Open the sample definition asset and verify that adding a modifier to the running sample still works (float HP modifier adds correctly).


## Concrete Steps

Run all commands from the repository root (`C:\Unity\Scaffold`) in PowerShell.

**After each milestone:**

    pwsh -NoProfile -File ".agents/scripts/validate-changes.ps1"

Expected output tail (96/96 on milestones 2–3; skip-tests on milestone 1 is also fine for a quick check):

    Tests: PASS
    Analyzers: PASS (TOTAL:0, BLOCKERS:0)
    Quality gates are clean. Changes are ready to commit.

**Milestone 1 visual check.** Open Unity, open `Assets/Packages/com.scaffold.entities/Samples/Authoring/SampleCharacterDefinition.asset`, expand entries — each entry must show two rows with no clipping, and assigning a new SO auto-creates the matching value type.


## Validation and Acceptance

- **Milestone 1:** Inspector shows two-row `AttributeEntry` layout. Assigning Float SO → `FloatAttributeValue` auto-created. `validate-changes.ps1 -SkipTests` exits 0.
- **Milestone 2:** `IEntity.TryGetAttribute` accepts `Attribute`; passing `AttributeSO hp` still compiles via implicit conversion. 96/96 EditMode tests pass.
- **Milestone 3:** `FloatAttributeValue.Combine`, `IntAttributeValue.Combine`, etc. each exist. `AttributeResolver.cs` and `AttributeCombine.cs` do not exist. 96/96 EditMode tests pass. `validate-changes.ps1` exits 0.


## Idempotence and Recovery

Each milestone is independently compilable and testable. If you abort mid-milestone, the worst state is a compile error in `EntityInstance.cs` (Milestone 3 step 3.4) — re-introducing the `resolver` field temporarily restores compilation while you finish the migration. No destructive asset changes occur; Unity serialized data (`SampleCharacterDefinition.asset`, etc.) is not modified by the code changes.


## Artifacts and Notes

**Implicit conversion in action (existing code, no change needed):**

    // attributeSO is type AttributeSO; after M2 this still compiles:
    entity.TryGetAttribute(attributeSO, out AttributeValue v); // implicit attributeSO → Attribute

**`Combine` contract example (FloatAttributeValue after M3):**

    public override AttributeValue Combine(IReadOnlyList<AttributeValue> contributions)
    {
        float sum = this.Value;
        for (int i = 0; i < contributions.Count; i++)
            if (contributions[i] is FloatAttributeValue f) sum += f.Value;
        float clamped = Math.Clamp(sum, this.Min, this.Max);
        return new FloatAttributeValue { Value = clamped, Min = this.Min, Max = this.Max };
    }

**`AttributeModifierHandler.GetEffective` outline:**

    internal AttributeValue GetEffective(Attribute key, AttributeValue baseValue)
    {
        if (baseValue == null) return null;
        var contributions = new List<AttributeValue>();
        for (int i = 0; i < modifiers.Count; i++)
        {
            EntityModifierEntry mod = modifiers[i];
            if (mod != null && mod.ModifierValue != null && mod.AttributeKey == key)
                contributions.Add(mod.ModifierValue);
        }
        return contributions.Count == 0 ? baseValue : baseValue.Combine(contributions);
    }


## Interfaces and Dependencies

After all milestones the key signatures are:

In `Runtime/Core/IEntity.cs`:

    public interface IEntity<out TDefinition> where TDefinition : EntityDefinition
    {
        InstanceId Id { get; }
        TDefinition Definition { get; }
        AttributeValue GetAttribute(Attribute attribute);
        T GetValue<T>(Attribute attribute);
        TAttr GetTypedAttribute<TAttr>(Attribute attribute) where TAttr : AttributeValue;
        bool TryGetAttribute(Attribute attribute, out AttributeValue value);
    }

In `Runtime/Core/AttributeValue.cs` (abstract base):

    public abstract class AttributeValue
    {
        public abstract AttributeValueType Type { get; }
        public abstract AttributeValue Combine(IReadOnlyList<AttributeValue> contributions);
    }

In `Runtime/Core/AttributeModifierHandler.cs` (internal, replaces AttributeResolver):

    internal sealed class AttributeModifierHandler
    {
        internal List<EntityModifierEntry> Modifiers { get; }
        internal void AddModifier(EntityModifierEntry entry);
        internal bool RemoveModifierAt(int index);
        internal void ClearModifiers();
        internal int Count { get; }
        internal AttributeValue GetEffective(Attribute key, AttributeValue baseValue);
    }

Files deleted by end of Milestone 3:
- `Assets/Packages/com.scaffold.entities/Runtime/Core/AttributeResolver.cs`
- `Assets/Packages/com.scaffold.entities/Runtime/Core/AttributeResolver.cs.meta`
- `Assets/Packages/com.scaffold.entities/Runtime/Core/AttributeCombine.cs`
- `Assets/Packages/com.scaffold.entities/Runtime/Core/AttributeCombine.cs.meta`
