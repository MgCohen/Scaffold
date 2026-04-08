# Clean up and simplify the Scaffold Entities package

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository policy for ExecPlans is defined in `PLANS.md` at the repository root. This document must be maintained in accordance with that file.


## Purpose / Big Picture

The entities package (`Assets/Packages/com.scaffold.entities/`) was implemented in a first pass (see `Plans/EntitiesExpand/EntitiesExpand-ExecPlan.md`, now complete). That pass established a working skeleton but left several design debts: attribute values are stored as raw strings, the entry class has a long confusing name, `EntityInstance` carries too many private helpers and two exposed dictionaries, the MonoBehaviour (`Entity`) is not generic, and the property drawer targets the wrong type. There is also no typed access surface — you cannot ask "give me the float value of this attribute" without parsing strings yourself.

After this work, every entity attribute carries a typed value object (`FloatAttributeValue`, `IntAttributeValue`, `BoolAttributeValue`, or `StringAttributeValue`) with optional numeric clamp constraints (min/max on float and int). The `AttributeSO` asset is a pure key with a type dropdown. A new covariant interface `IEntity<out TDefinition>` gives a clean, stable public surface, implemented by both the serializable `EntityInstance<TDefinition>` and the generic MonoBehaviour `EntityBehaviour<TDefinition>` — the MonoBehaviour delegates entirely to the instance, with no duplicated logic. The inspector property drawer for definition entries shows the attribute reference and value fields side by side. Tests are updated to exercise the typed get API.

You can see this working when: (1) the Unity inspector shows a type-picker dropdown for each attribute entry in a definition asset, (2) a float attribute entry shows `value`, `min`, `max` fields, (3) the new tests in `EntityInstanceTests.cs` pass, and (4) `validate-changes.ps1 -SkipTests` exits clean.


## Progress

- [x] Author initial ExecPlan at `Plans/EntitiesCleanup/EntitiesCleanup-ExecPlan.md` (this file).
- [x] Milestone 1 — Foundation types: `AttributeValueType` enum, `AttributeValue` abstract hierarchy, `Attribute` record class (pure key), `AttributeSO` simplified. Compiles clean.
- [x] Milestone 2 — Entry and definition update: `EntityDefinitionDefaultEntry` renamed to `AttributeEntry` with `[SerializeReference] AttributeValue baseValue`; `EntityModifierEntry` updated to `[SerializeReference] AttributeValue modifierValue`; `EntityDefinition` reduced to one runtime dictionary keyed by `Attribute`. Compiles clean.
- [x] Milestone 3 — `IEntity` interface, `EntityInstance<TDefinition>` made generic, `AttributeResolver` internal class introduced. All attribute/modifier logic moves here. Compiles clean.
- [x] Milestone 4 — `EntityBehaviour` abstract base, `EntityBehaviour<TDefinition>` generic MonoBehaviour (pure delegation), `EntityInstanceFactory` updated. Old `Entity.cs` and `EntityInstanceT.cs` deleted. Compiles clean.
- [x] Milestone 5 — `AttributePropertyDrawer` updated, `EntityInstanceTests.cs` written, all seven sample files updated, `SampleCharacterEntity` concrete subclass added. Tests pass.
- [x] Milestone 6 — Quality gate: `validate-changes.ps1 -SkipTests` clean; `README.md` updated.


## Surprises & Discoveries

- Unity-generated `.csproj` files list explicit `Compile` items; renames (`EntityInstanceState` → `EntityInstance`, etc.) required matching updates in `Scaffold.Entities.csproj`, `Scaffold.Entities.Tests.csproj`, and `Scaffold.Entities.Samples.csproj` or solution build fails with CS2001.
- `record` types need `IsExternalInit` when targeting Unity’s language version; added a minimal internal polyfill in `Runtime/Core/IsExternalInit.cs`.
- Analyzer **SCA3002** (one type per file) split the planned single `AttributeValue.cs` hierarchy into `FloatAttributeValue.cs`, `IntAttributeValue.cs`, `BoolAttributeValue.cs`, and `StringAttributeValue.cs`; **SCA3003** required `AttributeResolver` constructor before fields.
- Bool modifier combine in `AttributeResolver` applies last-write-wins on top of the base bool value (not only modifiers), matching the decision log intent.


## Decision Log

- Decision: `AttributeSO` is a pure key asset with no stored default value (Option A). Entities must always have a backing `EntityDefinition`; there is no meaningful attribute access without one.
  Rationale: Keeping default values only in `AttributeEntry` (on the definition) avoids having two separate places to set a canonical value. The SO's job is identity and type declaration, not value storage.
  Author: Planning session, 2026-04-07.

- Decision: `AttributeValue` hierarchy uses `[SerializeReference]` polymorphism. Concrete subtypes (`FloatAttributeValue`, `IntAttributeValue`, `BoolAttributeValue`, `StringAttributeValue`) are `[Serializable]` plain C# classes. Min/max live on the concrete numeric subtypes, not on `AttributeEntry`.
  Rationale: `[SerializeReference]` provides Unity-native polymorphism without ScriptableObject overhead. Min/max belong to the type's definition of valid range, so they belong on the value type.
  Author: Planning session, 2026-04-07.

- Decision: `Attribute` is a `record class` (pure key: `Key` string + `AttributeValueType Type`). It carries no value. `EntityInstance` uses it as the runtime dictionary key. The `AttributeSO` implicit conversion produces `new Attribute(so.name, so.ValueType)`.
  Rationale: The record's structural equality makes it safe as a dictionary key. Separating identity (record) from value (AttributeValue) clarifies the model.
  Author: Planning session, 2026-04-07.

- Decision: `EntityInstance<TDefinition>` runtime resolved cache is `Dictionary<Attribute, AttributeValue>` (not serialized). Only `TDefinition definition` and `List<EntityModifierEntry> modifiers` are serialized fields.
  Rationale: The resolved dict is derived data; serializing it would duplicate information already in the definition and modifiers. It is rebuilt on `Initialize` and after any modifier change.
  Author: Planning session, 2026-04-07.

- Decision: `AttributeResolver` is an `internal sealed class` that owns all combination logic. `EntityInstance` holds a resolver and calls `resolver.Recalculate(definition)` after any mutation. The public surface of `EntityInstance` is four groups: setup, get, modifier mutation, nothing else.
  Rationale: The user explicitly requested slimming the public surface. Moving enumerate/combine helpers into a single internal class gives a clean seam for future modifier model expansion.
  Author: Planning session, 2026-04-07.

- Decision: `Bool` modifier combination uses last-write-wins (the last modifier in list order replaces the base value).
  Rationale: Logical OR or AND require domain knowledge. Last-write-wins is safe and easy to reason about. The resolver's `Combine` method can be changed when a concrete use case demands a different rule.
  Author: Planning session, 2026-04-07.

- Decision: `AttributeCombine` (the existing internal static class that combines string payloads) is kept unchanged. `AttributeResolver` delegates to it for `String`-type attributes. It will be removed or replaced when the modifier model expands beyond string combination.
  Rationale: Keeping it avoids touching working code unnecessarily while still removing it from the public call path.
  Author: Planning session, 2026-04-07.

- Decision: `EntityBehaviour<TDefinition>` contains a `[SerializeField] EntityInstance<TDefinition> instance` and implements `IEntity<TDefinition>` by one-liner delegation to `instance.*`. No logic is duplicated in the MonoBehaviour layer.
  Rationale: The user stated the MonoBehaviour should just wire the interface. This keeps the only source of attribute logic in `EntityInstance`.
  Author: Planning session, 2026-04-07.

- Decision: A thin abstract non-generic `EntityBehaviour : MonoBehaviour` base is kept as the constraint target for `EntityBehaviorRunner<TData, TInput>` and `IEntityBehavior<TData, TInput>`. Heterogeneous list storage uses `IEntity<EntityDefinition>` via covariance; the base class problem is otherwise deferred.
  Rationale: The runner must constrain TData to something concrete. The abstract base costs almost nothing.
  Author: Planning session, 2026-04-07.

- Decision: `IEntity<out TDefinition>` is covariant. `GetValue<T>` and `GetTypedAttribute<TAttr>` are generic methods on a covariant interface, which is legal in C#.
  Rationale: Covariance lets `IEntity<SampleCharacterDefinition>` flow into a `List<IEntity<EntityDefinition>>` without casting. Generic instance methods do not conflict with output-position variance.
  Author: Planning session, 2026-04-07.


## Outcomes & Retrospective

- **Done:** Typed `AttributeValue` hierarchy with `[SerializeReference]` on definition/modifier entries; `IEntity<TDefinition>` with `GetValue<T>` / `GetTypedAttribute<TAttr>`; `EntityBehaviour` / `EntityBehaviour<TDefinition>` replace `Entity` / `EntityInstanceT`; internal `AttributeResolver` owns combine rules; package README and sample assets/prefab updated; `validate-changes.ps1 -SkipTests` passes with `TOTAL:0`.
- **Follow-up:** Run EditMode tests when Unity is available (`run-editmode-tests.ps1`); regenerate Unity `.csproj` from the editor if file lists drift after pull.


## Context and Orientation

### Repository structure

All work is inside `Assets/Packages/com.scaffold.entities/`. The sub-folders are:

    Runtime/
      Core/          — all domain types (11 .cs files today)
      Behavior/      — EntityBehaviorRunner, IEntityBehavior, IEntityFrameInputProvider
    Editor/          — AttributePropertyDrawer.cs
    Tests/           — EntityInstanceStateTests.cs (one file)
    Samples/         — 7 sample .cs files

The package is a standard Unity package with four `.asmdef` files. The runtime assembly (`Scaffold.Entities`) has `noEngineReferences: false` and references no other first-party assembly. The editor assembly references `Scaffold.Entities`. The test and samples assemblies also reference only `Scaffold.Entities`.

### Key terms used in this plan

An **`AttributeSO`** (ScriptableObject) is a Unity asset that acts as a named, typed slot identifier — think of it as a key that lives in the project. You create one for "HP", another for "MoveSpeed", etc. It now carries an `AttributeValueType` dropdown declaring what kind of data this attribute holds.

An **`Attribute` record** is a lightweight in-memory key produced from an `AttributeSO`. It holds the asset's name and type. It is never stored in serialized fields; it is created on demand and used as the runtime dictionary key in `EntityInstance`.

An **`AttributeValue`** is an abstract base class with four concrete subtypes: `FloatAttributeValue`, `IntAttributeValue`, `BoolAttributeValue`, and `StringAttributeValue`. Each carries the actual value (and min/max for numeric types). These are the objects stored in `AttributeEntry` and `EntityModifierEntry` via `[SerializeReference]` polymorphism.

An **`AttributeEntry`** is the serializable pair of (AttributeSO reference, AttributeValue) stored in the list on an `EntityDefinition`. It represents the base value of one attribute slot for that definition.

An **`EntityDefinition`** is a ScriptableObject that carries a list of `AttributeEntry` items. It is the shared "template" for entities. At runtime it builds a `Dictionary<Attribute, AttributeValue>` for fast lookup.

An **`EntityInstance<TDefinition>`** is a `[Serializable]` C# class (not a MonoBehaviour) that holds a reference to its definition, a unique `InstanceId`, and a list of `EntityModifierEntry` objects that override or add to the base values at runtime. It implements the `IEntity<TDefinition>` interface and contains all attribute resolution logic via an internal `AttributeResolver`.

An **`EntityBehaviour<TDefinition>`** is a MonoBehaviour that owns an `EntityInstance<TDefinition>` as a serialized field and delegates every `IEntity<TDefinition>` method call to it.

A **`[SerializeReference]`** field is a Unity serialization mechanism that stores a C# object reference polymorphically — Unity will show a type-selector dropdown in the inspector for the field.

### Current state of each affected file

Before this plan executes the package contains the following files (abbreviated to the parts that change):

`Runtime/Core/Attribute.cs` — a `[Serializable] struct Attribute` with `string payload` (property `Payload`) and `string matchKey` (property `MatchKey`); manual equality, hash, and operators; `#nullable enable`.

`Runtime/Core/AttributeSO.cs` — `ScriptableObject` with `[SerializeField] string defaultPayload`; property `DefaultPayload`; implicit conversion to `Attribute` using `DefaultPayload` and `so.name`.

`Runtime/Core/EntityDefinitionDefaultEntry.cs` — class `EntityDefinitionDefaultEntry`; fields `AttributeSO attribute` and `string payloadOverride`; method `GetDefaultAttribute()` returning `Attribute`.

`Runtime/Core/EntityModifierEntry.cs` — `[Serializable] sealed class EntityModifierEntry`; fields `AttributeSO attribute` and `string contribution`; properties `Attribute` and `Contribution`.

`Runtime/Core/EntityDefinition.cs` — `ScriptableObject`; `List<EntityDefinitionDefaultEntry> defaultAttributes`; two dictionaries `attributeToEntry` and `nameToAttribute`; methods `RebuildLookup`, `GetBaseAttribute`, `TryGetAttributeSOByName`, `TryGetDefaultEntry`.

`Runtime/Core/EntityInstanceState.cs` — `[Serializable] sealed class EntityInstanceState`; fields `InstanceId id`, `EntityDefinition definition`, `List<EntityModifierEntry> modifiers`; public methods `Initialize`, `EnsureDefinitionLookup`, three `TryGetAttribute` overloads, `AddModifier`, `ClearModifiers`, `RemoveModifierAt`; private helpers `TryFindAttributeByStringScan`, `TryMatchSlotByString`, `GetEffectiveAttribute`, `CollectContributions`, `EnumerateAttributeSlots`, `EnumerateDefinitionSlots`, `EnumerateModifierSlots`, `MatchesStringQuery`.

`Runtime/Core/Entity.cs` — `MonoBehaviour Entity`; `[SerializeField] EntityInstanceState instanceState`; wraps `instanceState` with pass-through methods including three `TryGetAttribute` overloads.

`Runtime/Core/EntityInstanceT.cs` — `class EntityInstance<TDefinition> : Entity where TDefinition : EntityDefinition`; only purpose: `new TDefinition Definition` property with a cast.

`Runtime/Core/EntityInstanceFactory.cs` — `static EntityInstanceFactory`; methods `CreateState(EntityDefinition)` and `CreateOnGameObject<TEntity>(GameObject, EntityDefinition)`.

`Runtime/Core/AttributeCombine.cs` — `internal static AttributeCombine`; `Combine(string basePayload, List<string>)` that tries float-sum then falls back to concatenation.

`Editor/AttributePropertyDrawer.cs` — `[CustomPropertyDrawer(typeof(Attribute))]`; draws `payload` and `matchKey` as two text fields side by side.

`Tests/EntityInstanceStateTests.cs` — NUnit tests using reflection to set private fields; asserts `TryGetAttribute` overloads and modifier behavior; uses `Attribute.Payload`.

`Samples/*.cs` — seven files; use `Entity`, `Attribute.Payload`, string-based access.


## Plan of Work

The plan is structured as six milestones. Each milestone results in a compilable state and can be committed independently. The milestones are ordered so that later milestones depend only on earlier ones.

### Milestone 1 — Foundation types

This milestone introduces the new type vocabulary. Nothing is deleted yet; old types still exist. Goal: the new types compile alongside the old ones.

**Step 1.1 — `AttributeValueType` enum.** Create `Assets/Packages/com.scaffold.entities/Runtime/Core/AttributeValueType.cs` via Unity MCP (to get a valid `.meta` file). Contents:

    namespace Scaffold.Entities
    {
        public enum AttributeValueType
        {
            String,
            Float,
            Int,
            Bool
        }
    }

**Step 1.2 — `AttributeValue` hierarchy.** Create `Assets/Packages/com.scaffold.entities/Runtime/Core/AttributeValue.cs` via Unity MCP. The file contains the abstract base and all four concrete subtypes. Put all five classes in the same file to keep the hierarchy colocated:

    using System;
    using UnityEngine;

    namespace Scaffold.Entities
    {
        [Serializable]
        public abstract class AttributeValue
        {
            public abstract AttributeValueType Type { get; }
        }

        [Serializable]
        public sealed class FloatAttributeValue : AttributeValue
        {
            [SerializeField] public float value;
            [SerializeField] public float min = float.MinValue;
            [SerializeField] public float max = float.MaxValue;
            public override AttributeValueType Type => AttributeValueType.Float;
        }

        [Serializable]
        public sealed class IntAttributeValue : AttributeValue
        {
            [SerializeField] public int value;
            [SerializeField] public int min = int.MinValue;
            [SerializeField] public int max = int.MaxValue;
            public override AttributeValueType Type => AttributeValueType.Int;
        }

        [Serializable]
        public sealed class BoolAttributeValue : AttributeValue
        {
            [SerializeField] public bool value;
            public override AttributeValueType Type => AttributeValueType.Bool;
        }

        [Serializable]
        public sealed class StringAttributeValue : AttributeValue
        {
            [SerializeField] public string value = string.Empty;
            public override AttributeValueType Type => AttributeValueType.String;
        }
    }

**Step 1.3 — Rewrite `Attribute.cs`.** Replace the entire contents of `Assets/Packages/com.scaffold.entities/Runtime/Core/Attribute.cs`. The `struct` becomes a `record class`. The old `Payload`/`MatchKey` properties and all manual equality/hash/operator code are removed:

    #nullable enable
    namespace Scaffold.Entities
    {
        public record Attribute(string Key, AttributeValueType Type = AttributeValueType.String);
    }

This breaks `AttributeSO.cs` and `EntityInstanceState.cs` which still reference `Attribute.Payload`/`Attribute.MatchKey`. That is expected — they are fixed in subsequent steps.

**Step 1.4 — Update `AttributeSO.cs`.** Replace the contents of `Assets/Packages/com.scaffold.entities/Runtime/Core/AttributeSO.cs`:

    using UnityEngine;

    namespace Scaffold.Entities
    {
        [CreateAssetMenu(menuName = "Scaffold/Entity/Attribute", fileName = "Attribute")]
        public class AttributeSO : ScriptableObject
        {
            public AttributeValueType ValueType => valueType;

            [SerializeField]
            private AttributeValueType valueType = AttributeValueType.String;

            public static implicit operator Attribute(AttributeSO so)
            {
                if (so == null)
                {
                    return new Attribute(string.Empty);
                }

                return new Attribute(so.name, so.ValueType);
            }
        }
    }

Validate that `Scaffold.Entities` compiles after this step (the old files that still reference the old `Attribute` struct will fail; that is the expected state entering Milestone 2).

### Milestone 2 — Entry and definition update

This milestone updates the data-holder types to use `AttributeValue` and removes the old string fields.

**Step 2.1 — Rename `EntityDefinitionDefaultEntry` to `AttributeEntry`.** Use Unity MCP to rename the file (preserves the `.meta` GUID). In the new `AttributeEntry.cs`, replace the class contents entirely:

    using System;
    using UnityEngine;

    namespace Scaffold.Entities
    {
        [Serializable]
        public sealed class AttributeEntry
        {
            public AttributeSO Attribute => attribute;

            [SerializeField]
            private AttributeSO attribute = default!;

            public AttributeValue BaseValue => baseValue;

            [SerializeReference]
            private AttributeValue baseValue;
        }
    }

The old `payloadOverride` string and `GetDefaultAttribute()` method are removed entirely.

**Step 2.2 — Update `EntityModifierEntry.cs`.** Replace the `contribution` string field with a `[SerializeReference] AttributeValue modifierValue` field. Remove the `Contribution` property. Add `ModifierValue`:

    using System;
    using UnityEngine;

    namespace Scaffold.Entities
    {
        [Serializable]
        public sealed class EntityModifierEntry
        {
            public EntityModifierEntry(AttributeSO attribute, AttributeValue modifierValue)
            {
                this.attribute = attribute;
                this.modifierValue = modifierValue;
            }

            public EntityModifierEntry() { }

            public AttributeSO Attribute => attribute;

            [SerializeField]
            private AttributeSO attribute = default!;

            public AttributeValue ModifierValue => modifierValue;

            [SerializeReference]
            private AttributeValue modifierValue;
        }
    }

**Step 2.3 — Update `EntityDefinition.cs`.** Replace the two dictionaries (`attributeToEntry`, `nameToAttribute`) with one `Dictionary<Attribute, AttributeValue> baseValues`. Remove `GetBaseAttribute`, `TryGetAttributeSOByName`, `TryGetDefaultEntry`. Add `TryGetBaseValue(Attribute, out AttributeValue)`. Fix any analyzer formatting issues (null-conditional on class types, method order). The list field is renamed from `defaultAttributes` to `entries` and the property from `DefaultAttributes` to `Entries`:

    using System.Collections.Generic;
    using UnityEngine;

    namespace Scaffold.Entities
    {
        public class EntityDefinition : ScriptableObject
        {
            public IReadOnlyList<AttributeEntry> Entries => entries;

            [SerializeField]
            private List<AttributeEntry> entries = new List<AttributeEntry>();

            private readonly Dictionary<Attribute, AttributeValue> baseValues =
                new Dictionary<Attribute, AttributeValue>();

            private void OnEnable()
            {
                RebuildLookup();
            }

            private void OnValidate()
            {
                RebuildLookup();
            }

            public void RebuildLookup()
            {
                baseValues.Clear();
                for (int i = 0; i < entries.Count; i++)
                {
                    AttributeEntry entry = entries[i];
                    if (entry == null || entry.Attribute == null)
                    {
                        continue;
                    }

                    Attribute key = (Attribute)entry.Attribute;
                    baseValues[key] = entry.BaseValue;
                }
            }

            public bool TryGetBaseValue(Attribute key, out AttributeValue value)
            {
                return baseValues.TryGetValue(key, out value);
            }
        }
    }

Validate compilation after this step. The `EntityInstanceState.cs` and `Entity.cs` files still reference the old API and will fail; that is expected.

### Milestone 3 — IEntity interface, EntityInstance generic, AttributeResolver

This milestone introduces the new get/resolve surface and wires everything together.

**Step 3.1 — Create `IEntity.cs`.** Use Unity MCP to create `Assets/Packages/com.scaffold.entities/Runtime/Core/IEntity.cs`:

    namespace Scaffold.Entities
    {
        public interface IEntity<out TDefinition> where TDefinition : EntityDefinition
        {
            InstanceId Id { get; }

            TDefinition Definition { get; }

            AttributeValue GetAttribute(AttributeSO attribute);

            T GetValue<T>(AttributeSO attribute);

            TAttr GetTypedAttribute<TAttr>(AttributeSO attribute) where TAttr : AttributeValue;

            bool TryGetAttribute(AttributeSO attribute, out AttributeValue value);
        }
    }

**Step 3.2 — Create `AttributeResolver.cs`.** Use Unity MCP to create `Assets/Packages/com.scaffold.entities/Runtime/Core/AttributeResolver.cs`. This class is `internal` and owns all attribute combination logic:

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    namespace Scaffold.Entities
    {
        internal sealed class AttributeResolver
        {
            private readonly List<EntityModifierEntry> modifiers;
            private readonly Dictionary<Attribute, AttributeValue> resolved =
                new Dictionary<Attribute, AttributeValue>();

            internal AttributeResolver(List<EntityModifierEntry> modifiers)
            {
                this.modifiers = modifiers ?? throw new ArgumentNullException(nameof(modifiers));
            }

            internal void Recalculate(EntityDefinition definition)
            {
                resolved.Clear();
                if (definition == null)
                {
                    return;
                }

                IReadOnlyList<AttributeEntry> entries = definition.Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    AttributeEntry entry = entries[i];
                    if (entry == null || entry.Attribute == null)
                    {
                        continue;
                    }

                    Attribute key = (Attribute)entry.Attribute;
                    resolved[key] = Combine(key, entry.BaseValue);
                }
            }

            internal bool TryGetValue(Attribute key, out AttributeValue value)
            {
                return resolved.TryGetValue(key, out value);
            }

            private AttributeValue Combine(Attribute key, AttributeValue baseValue)
            {
                List<AttributeValue> contributions = CollectModifiers(key);
                if (contributions.Count == 0)
                {
                    return baseValue;
                }

                return baseValue switch
                {
                    FloatAttributeValue f => CombineFloat(f, contributions),
                    IntAttributeValue n => CombineInt(n, contributions),
                    BoolAttributeValue _ => CombineBool(contributions),
                    StringAttributeValue s => CombineString(s, contributions),
                    _ => baseValue
                };
            }

            private List<AttributeValue> CollectModifiers(Attribute key)
            {
                var result = new List<AttributeValue>();
                for (int i = 0; i < modifiers.Count; i++)
                {
                    EntityModifierEntry mod = modifiers[i];
                    if (mod == null || mod.Attribute == null)
                    {
                        continue;
                    }

                    Attribute modKey = (Attribute)mod.Attribute;
                    if (modKey == key)
                    {
                        result.Add(mod.ModifierValue);
                    }
                }

                return result;
            }

            private static FloatAttributeValue CombineFloat(FloatAttributeValue baseVal, List<AttributeValue> contributions)
            {
                float sum = baseVal.value;
                for (int i = 0; i < contributions.Count; i++)
                {
                    if (contributions[i] is FloatAttributeValue f)
                    {
                        sum += f.value;
                    }
                }

                float clamped = Math.Clamp(sum, baseVal.min, baseVal.max);
                return new FloatAttributeValue { value = clamped, min = baseVal.min, max = baseVal.max };
            }

            private static IntAttributeValue CombineInt(IntAttributeValue baseVal, List<AttributeValue> contributions)
            {
                int sum = baseVal.value;
                for (int i = 0; i < contributions.Count; i++)
                {
                    if (contributions[i] is IntAttributeValue n)
                    {
                        sum += n.value;
                    }
                }

                int clamped = Math.Clamp(sum, baseVal.min, baseVal.max);
                return new IntAttributeValue { value = clamped, min = baseVal.min, max = baseVal.max };
            }

            private static BoolAttributeValue CombineBool(List<AttributeValue> contributions)
            {
                // Last modifier wins.
                BoolAttributeValue last = null;
                for (int i = 0; i < contributions.Count; i++)
                {
                    if (contributions[i] is BoolAttributeValue b)
                    {
                        last = b;
                    }
                }

                return last ?? new BoolAttributeValue();
            }

            private static StringAttributeValue CombineString(StringAttributeValue baseVal, List<AttributeValue> contributions)
            {
                var stringContribs = new List<string>(contributions.Count);
                for (int i = 0; i < contributions.Count; i++)
                {
                    if (contributions[i] is StringAttributeValue s)
                    {
                        stringContribs.Add(s.value);
                    }
                }

                string combined = AttributeCombine.Combine(baseVal.value, stringContribs);
                return new StringAttributeValue { value = combined };
            }
        }
    }

**Step 3.3 — Rewrite `EntityInstanceState.cs` as `EntityInstance.cs`.** Use Unity MCP to rename the file. Replace the entire class:

    using System;
    using System.Collections.Generic;
    using UnityEngine;

    namespace Scaffold.Entities
    {
        [Serializable]
        public sealed class EntityInstance<TDefinition> : IEntity<TDefinition>
            where TDefinition : EntityDefinition
        {
            public InstanceId Id => id;

            [SerializeField]
            private InstanceId id;

            public TDefinition Definition => definition;

            [SerializeField]
            private TDefinition definition = default!;

            [SerializeField]
            private List<EntityModifierEntry> modifiers = new List<EntityModifierEntry>();

            private AttributeResolver resolver;

            public void Initialize(InstanceId instanceId, TDefinition entityDefinition)
            {
                id = instanceId;
                definition = entityDefinition ?? throw new ArgumentNullException(nameof(entityDefinition));
                modifiers = new List<EntityModifierEntry>();
                resolver = new AttributeResolver(modifiers);
                resolver.Recalculate(definition);
            }

            public AttributeValue GetAttribute(AttributeSO attribute)
            {
                EnsureResolver();
                Attribute key = (Attribute)attribute;
                resolver.TryGetValue(key, out AttributeValue value);
                return value;
            }

            public T GetValue<T>(AttributeSO attribute)
            {
                AttributeValue av = GetAttribute(attribute);
                return av switch
                {
                    FloatAttributeValue f when typeof(T) == typeof(float) => (T)(object)f.value,
                    IntAttributeValue n when typeof(T) == typeof(int)     => (T)(object)n.value,
                    BoolAttributeValue b when typeof(T) == typeof(bool)   => (T)(object)b.value,
                    StringAttributeValue s when typeof(T) == typeof(string) => (T)(object)s.value,
                    _ => throw new InvalidCastException(
                        $"Attribute '{attribute?.name}' has type {av?.Type} but {typeof(T).Name} was requested.")
                };
            }

            public TAttr GetTypedAttribute<TAttr>(AttributeSO attribute) where TAttr : AttributeValue
            {
                AttributeValue av = GetAttribute(attribute);
                if (av is TAttr typed)
                {
                    return typed;
                }

                throw new InvalidCastException(
                    $"Attribute '{attribute?.name}' is {av?.GetType().Name} but {typeof(TAttr).Name} was requested.");
            }

            public bool TryGetAttribute(AttributeSO attribute, out AttributeValue value)
            {
                EnsureResolver();
                Attribute key = (Attribute)attribute;
                return resolver.TryGetValue(key, out value);
            }

            public void AddModifier(EntityModifierEntry entry)
            {
                if (entry == null)
                {
                    return;
                }

                EnsureResolver();
                modifiers.Add(entry);
                resolver.Recalculate(definition);
            }

            public bool RemoveModifierAt(int index)
            {
                if (index < 0 || index >= modifiers.Count)
                {
                    return false;
                }

                modifiers.RemoveAt(index);
                resolver.Recalculate(definition);
                return true;
            }

            public void ClearModifiers()
            {
                modifiers.Clear();
                resolver?.Recalculate(definition);
            }

            private void EnsureResolver()
            {
                if (resolver == null)
                {
                    resolver = new AttributeResolver(modifiers);
                    resolver.Recalculate(definition);
                }
            }
        }
    }

`EnsureResolver` handles the case where Unity deserialized the instance (setting serialized fields) without calling `Initialize` — the resolver is a non-serialized field and must be rebuilt after deserialization.

Validate compilation after this step. `Entity.cs` and `EntityInstanceT.cs` still reference old types; those are fixed in Milestone 4.

### Milestone 4 — EntityBehaviour restructure and factory update

**Step 4.1 — Rewrite `Entity.cs` as `EntityBehaviour.cs`.** Use Unity MCP to rename. The new `EntityBehaviour.cs` contains only the abstract base:

    using UnityEngine;

    namespace Scaffold.Entities
    {
        public abstract class EntityBehaviour : MonoBehaviour
        {
        }
    }

**Step 4.2 — Create `EntityBehaviourT.cs`.** Use Unity MCP to create `Assets/Packages/com.scaffold.entities/Runtime/Core/EntityBehaviourT.cs`. This replaces `EntityInstanceT.cs` (deleted in Step 4.3):

    using UnityEngine;

    namespace Scaffold.Entities
    {
        public class EntityBehaviour<TDefinition> : EntityBehaviour, IEntity<TDefinition>
            where TDefinition : EntityDefinition
        {
            public EntityInstance<TDefinition> Instance => instance;

            [SerializeField]
            private EntityInstance<TDefinition> instance = new EntityInstance<TDefinition>();

            private void Awake()
            {
                instance.EnsureResolver(); // handled internally via EnsureResolver in EntityInstance
            }

            public void InitializeFromDefinition(TDefinition definition)
            {
                instance.Initialize(InstanceId.New(), definition);
            }

            // IEntity<TDefinition> — pure delegation

            public InstanceId Id => instance.Id;

            public TDefinition Definition => instance.Definition;

            public AttributeValue GetAttribute(AttributeSO attribute)
                => instance.GetAttribute(attribute);

            public T GetValue<T>(AttributeSO attribute)
                => instance.GetValue<T>(attribute);

            public TAttr GetTypedAttribute<TAttr>(AttributeSO attribute) where TAttr : AttributeValue
                => instance.GetTypedAttribute<TAttr>(attribute);

            public bool TryGetAttribute(AttributeSO attribute, out AttributeValue value)
                => instance.TryGetAttribute(attribute, out value);

            // Modifier pass-throughs

            public void AddModifier(EntityModifierEntry entry)
                => instance.AddModifier(entry);

            public bool RemoveModifierAt(int index)
                => instance.RemoveModifierAt(index);

            public void ClearModifiers()
                => instance.ClearModifiers();
        }
    }

Note: `EnsureResolver` is private in `EntityInstance`. The `Awake` in `EntityBehaviour<TDefinition>` calls `TryGetAttribute` with a null check to trigger lazy initialization, or — simpler — just exposes a public `EnsureReady()` / relies on `EnsureResolver` being triggered by the first `GetAttribute` call. Remove the `Awake` call or make `EnsureResolver` internal. Update the plan if you choose to expose it.

**Step 4.3 — Delete `EntityInstanceT.cs`.** Use Unity MCP to delete both the `.cs` file and its `.meta` file.

**Step 4.4 — Update `EntityBehaviorRunner.cs`.** The constraint `where TData : Entity` becomes `where TData : EntityBehaviour`. Update the field type and all references inside the file. Update `IEntityBehavior.cs` and `IEntityFrameInputProvider.cs` similarly if they reference `Entity`.

**Step 4.5 — Update `EntityInstanceFactory.cs`.** Rename `CreateState` to `CreateInstance`. Make it generic. Update `CreateOnGameObject`:

    using System;
    using System.Collections.Generic;
    using UnityEngine;

    namespace Scaffold.Entities
    {
        public static class EntityInstanceFactory
        {
            public static EntityInstance<TDefinition> CreateInstance<TDefinition>(TDefinition definition)
                where TDefinition : EntityDefinition
            {
                if (definition == null)
                {
                    throw new ArgumentNullException(nameof(definition));
                }

                var instance = new EntityInstance<TDefinition>();
                instance.Initialize(InstanceId.New(), definition);
                return instance;
            }

            public static TEntity CreateOnGameObject<TEntity, TDefinition>(
                GameObject gameObject,
                TDefinition definition)
                where TEntity : EntityBehaviour<TDefinition>
                where TDefinition : EntityDefinition
            {
                if (gameObject == null)
                {
                    throw new ArgumentNullException(nameof(gameObject));
                }

                TEntity entity = gameObject.AddComponent<TEntity>();
                entity.InitializeFromDefinition(definition);
                return entity;
            }
        }
    }

Validate compilation. All runtime types should compile cleanly at this point. The editor and samples may still fail; that is addressed in Milestone 5.

### Milestone 5 — Property drawer, tests, and samples

**Step 5.1 — Update `AttributePropertyDrawer.cs`.** The drawer now targets `AttributeEntry` (the list element in `EntityDefinition`). It shows the `attribute` SO reference field and the `baseValue` SerializeReference field side by side on one line:

    using Scaffold.Entities;
    using UnityEditor;
    using UnityEngine;

    namespace Scaffold.Entities.Editor
    {
        [CustomPropertyDrawer(typeof(AttributeEntry))]
        public sealed class AttributePropertyDrawer : PropertyDrawer
        {
            private const float gap = 4f;

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                SerializedProperty attributeProp = property.FindPropertyRelative("attribute");
                SerializedProperty valueProp = property.FindPropertyRelative("baseValue");

                float line = EditorGUIUtility.singleLineHeight;
                Rect row = EditorGUI.PrefixLabel(position, label);
                float half = (row.width - gap) * 0.5f;
                var soRect = new Rect(row.x, row.y, half, line);
                var valRect = new Rect(row.x + half + gap, row.y, half, line);

                EditorGUI.BeginProperty(position, label, property);
                EditorGUI.PropertyField(soRect, attributeProp, GUIContent.none);
                EditorGUI.PropertyField(valRect, valueProp, GUIContent.none);
                EditorGUI.EndProperty();
            }
        }
    }

The old `[CustomPropertyDrawer(typeof(Attribute))]` drawer code is removed. Since `Attribute` is now a record class (not a serialized struct), it no longer appears in the inspector as a serialized property.

**Step 5.2 — Update the test file.** Use Unity MCP to rename `EntityInstanceStateTests.cs` to `EntityInstanceTests.cs`. Rewrite its content to match the new API. Key changes: reflection helper creates `FloatAttributeValue` objects instead of strings; uses `GetValue<float>` for assertions; no `Payload` references. The test class is `EntityInstanceTests`. Example test for float modifier:

    [Test]
    public void Modifiers_OnInstance_SumFloatValues()
    {
        AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
        EntityDefinition def = CreateDefinition((hp, new FloatAttributeValue { value = 10f }));
        EntityInstance<EntityDefinition> state = EntityInstanceFactory.CreateInstance(def);
        state.AddModifier(new EntityModifierEntry(hp, new FloatAttributeValue { value = 5f }));

        Assert.That(state.GetValue<float>(hp), Is.EqualTo(15f));
    }

All existing test scenarios (base value, modifier combination, clear modifiers, factory id, etc.) must be rewritten for the new API. The reflection-based helper `CreateAttributeSo` now sets the `valueType` field; `CreateDefinition` sets `entries` (renamed from `defaultAttributes`) and uses `AttributeEntry` with `baseValue`.

**Step 5.3 — Add `SampleCharacterEntity.cs`.** The samples need a concrete non-generic MonoBehaviour for the prefab. Create `Assets/Packages/com.scaffold.entities/Samples/SampleCharacterEntity.cs` via Unity MCP:

    namespace Scaffold.Entities.Samples
    {
        public sealed class SampleCharacterEntity : EntityBehaviour<SampleCharacterDefinition>
        {
        }
    }

**Step 5.4 — Update the seven existing sample files.** In each file, replace `Entity` type references with `SampleCharacterEntity` or `EntityBehaviour<SampleCharacterDefinition>` as appropriate. Replace `entity.TryGetAttribute(slot, out Attribute a)` with `entity.TryGetAttribute(slot, out AttributeValue a)`. Replace `a.Payload` / `value.Payload` with `entity.GetValue<float>(slot)` or `entity.GetValue<string>(slot)` depending on the declared type. Update `SampleCharacterBehaviorRunner` to `EntityBehaviorRunner<SampleCharacterEntity, SampleCharacterInput>`.

Run the Unity EditMode tests via `.agents/scripts/run-editmode-tests.ps1` from the repository root and confirm all tests in `Scaffold.Entities.Tests` pass.

### Milestone 6 — Quality gate and documentation

Run the validation script from the repository root:

    pwsh -NoProfile -File ".agents/scripts/validate-changes.ps1" -SkipTests

Fix any diagnostics reported. Common issues to check: method order violations (SCA2001/SCA2003), null-conditional on class types, missing braces. Re-run until the gate shows `TOTAL:0`.

Update `Assets/Packages/com.scaffold.entities/README.md` to reflect: typed attribute values, the `IEntity<TDefinition>` interface, the `GetValue<T>` / `GetTypedAttribute<TAttr>` API, and the fact that `EntityBehaviour<TDefinition>` is the MonoBehaviour entry point. Remove references to `Attribute.Payload`, `EntityInstanceState`, and `Entity`.

Commit the changes with a message summarizing the milestone batch.


## Concrete Steps

All commands run from the repository root `c:\Unity\Scaffold` unless stated otherwise.

Create the target directory and ExecPlan file (already done):

    New-Item -ItemType Directory -Path "Plans/EntitiesCleanup" -Force

Validation command (run after each milestone):

    pwsh -NoProfile -File ".agents/scripts/validate-changes.ps1" -SkipTests

EditMode tests (run before Milestone 6 commit):

    pwsh -NoProfile -File ".agents/scripts/run-editmode-tests.ps1" -TestPlatform EditMode

The validation script reports `TOTAL:0` on success. Any non-zero count lists the failing diagnostics with file and line numbers — fix each one before continuing.

When creating or renaming files under `Assets/`, use Unity MCP (`user-unity-mcp`) to ensure `.meta` files are created with valid 32-hex-character GUIDs. If Unity MCP is unavailable, create `.meta` files manually, copying the YAML shape of an existing sibling `.meta` file in the same folder and generating a fresh 32-character hex GUID (e.g., `System.Guid.NewGuid().ToString("N")`).


## Validation and Acceptance

The change is complete and correct when all of the following hold:

1. `validate-changes.ps1 -SkipTests` reports `TOTAL:0` from the repository root.
2. All tests in `Scaffold.Entities.Tests` pass when run via `run-editmode-tests.ps1 -TestPlatform EditMode`. The test `Modifiers_OnInstance_SumFloatValues` (and its siblings) pass. No test named `*EntityInstanceState*` exists.
3. Opening a `SampleCharacterDefinition` asset in the Unity inspector shows a list where each row has a dropdown type-picker next to the attribute SO reference, and float entries show `value`, `min`, `max` fields.
4. An `AttributeSO` asset shows a `Value Type` dropdown in the inspector (String / Float / Int / Bool).
5. No file named `EntityDefinitionDefaultEntry.cs`, `EntityInstanceState.cs`, `EntityInstanceT.cs`, or `Entity.cs` exists in the `Runtime/Core/` folder.


## Idempotence and Recovery

Each milestone ends in a compilable state. If work is interrupted mid-milestone, restore to the last clean commit and re-apply only the incomplete steps. File renames via Unity MCP are safe to re-run if the old file still exists; if the rename already happened, skip the rename step and proceed to content edits.

If `[SerializeReference]` fields produce null values when opening the project after a rename (because old serialized data referenced the old class name), the assets will show `[Missing Reference]` in the inspector. Fix by opening each affected asset and reassigning the entries — no code change is needed.


## Artifacts and Notes

Key file paths after all milestones complete:

    Runtime/Core/
      AttributeValueType.cs       — new enum
      AttributeValue.cs           — new abstract base + 4 concrete subtypes
      Attribute.cs                — rewritten as record class (Key, Type)
      AttributeSO.cs              — simplified, ValueType dropdown
      AttributeEntry.cs           — renamed from EntityDefinitionDefaultEntry
      EntityModifierEntry.cs      — modifierValue replaces contribution
      EntityDefinition.cs         — one dictionary, Entries property
      IEntity.cs                  — new covariant interface
      EntityInstance.cs           — renamed from EntityInstanceState, now generic
      AttributeResolver.cs        — new internal class
      AttributeCombine.cs         — unchanged
      EntityBehaviour.cs          — renamed from Entity, now abstract base
      EntityBehaviourT.cs         — new generic MonoBehaviour (was EntityInstanceT)
      EntityInstanceFactory.cs    — updated for generics
      InstanceId.cs               — unchanged
    Editor/
      AttributePropertyDrawer.cs  — targets AttributeEntry
    Tests/
      EntityInstanceTests.cs      — renamed, rewritten
    Samples/
      SampleCharacterEntity.cs    — new concrete subclass for prefab
      (6 existing samples)        — updated names and API


## Interfaces and Dependencies

Final public signatures that must exist at the end of this plan:

In `Runtime/Core/IEntity.cs`:

    public interface IEntity<out TDefinition> where TDefinition : EntityDefinition
    {
        InstanceId Id { get; }
        TDefinition Definition { get; }
        AttributeValue GetAttribute(AttributeSO attribute);
        T GetValue<T>(AttributeSO attribute);
        TAttr GetTypedAttribute<TAttr>(AttributeSO attribute) where TAttr : AttributeValue;
        bool TryGetAttribute(AttributeSO attribute, out AttributeValue value);
    }

In `Runtime/Core/EntityInstance.cs`:

    public sealed class EntityInstance<TDefinition> : IEntity<TDefinition>
        where TDefinition : EntityDefinition
    {
        public InstanceId Id { get; }
        public TDefinition Definition { get; }
        public void Initialize(InstanceId instanceId, TDefinition entityDefinition);
        public AttributeValue GetAttribute(AttributeSO attribute);
        public T GetValue<T>(AttributeSO attribute);
        public TAttr GetTypedAttribute<TAttr>(AttributeSO attribute) where TAttr : AttributeValue;
        public bool TryGetAttribute(AttributeSO attribute, out AttributeValue value);
        public void AddModifier(EntityModifierEntry entry);
        public bool RemoveModifierAt(int index);
        public void ClearModifiers();
    }

In `Runtime/Core/EntityBehaviourT.cs`:

    public class EntityBehaviour<TDefinition> : EntityBehaviour, IEntity<TDefinition>
        where TDefinition : EntityDefinition
    {
        public EntityInstance<TDefinition> Instance { get; }
        public void InitializeFromDefinition(TDefinition definition);
        // IEntity<TDefinition> members — all delegated to Instance
        // Modifier pass-throughs — all delegated to Instance
    }

In `Runtime/Core/EntityInstanceFactory.cs`:

    public static class EntityInstanceFactory
    {
        public static EntityInstance<TDefinition> CreateInstance<TDefinition>(TDefinition definition)
            where TDefinition : EntityDefinition;
        public static TEntity CreateOnGameObject<TEntity, TDefinition>(GameObject go, TDefinition definition)
            where TEntity : EntityBehaviour<TDefinition>
            where TDefinition : EntityDefinition;
    }
