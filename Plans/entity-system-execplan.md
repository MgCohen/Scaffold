# Build a Core Entity System with Unity Wrappers and Global Registry

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

After this work, the project will have a reusable entity system where game data is modeled as plain serializable C# classes with no Unity dependency, while Unity-facing wrappers expose the same data through ScriptableObjects. A contributor will be able to register entity definitions and instances globally, fetch instances by id, and evaluate attribute values with temporary modifiers.

The user-visible behavior is: create an entity definition with base attributes, create an entity instance generically bound to that definition type, attach temporary modifiers implemented as concrete behavior classes, and query attributes so that unmodified values return directly from definition data while modified values return computed results.

## Progress

- [x] (2026-03-10 03:00Z) Created `Plans/entity-system-execplan.md` with full scope, milestones, and acceptance criteria.
- [x] (2026-03-10 19:05Z) Milestone 0 completed: created `Core/Entities` and `Presentation/Entities` runtime/tests/samples modules with asmdefs and baseline compile.
- [x] (2026-03-10 19:10Z) Milestone 1 completed: core serializable models (`Definition`, generic `Instance`, `Attribute`, abstract/concrete `Modifier`) implemented with tests.
- [x] (2026-03-10 19:12Z) Milestone 2 completed: attribute resolution path with unmodified fast path and polymorphic modifiers implemented and tested.
- [x] (2026-03-10 19:14Z) Milestone 3 completed: global registry with shared uniqueness gate across definitions/instances implemented and tested.
- [x] (2026-03-10 19:18Z) Milestone 4 completed: Unity wrapper module (`ScriptableObject` adapters with conversion operators) implemented and tested.
- [x] (2026-03-10 19:24Z) Milestone 5 completed: docs and samples added; analyzers and EditMode tests clean.

## Surprises & Discoveries

- Observation: `Scaffold.Entities` can use `noEngineReferences: true` while still integrating cleanly with Unity EditMode test assemblies that reference the runtime assembly.
  Evidence: `Scaffold.Entities` runtime asmdef uses `noEngineReferences: true` and test run passed (`49/49`).

- Observation: `validate-milestone.ps1` currently fails due a script-level `Resolve-Path` issue (`-ProjectPath` interpreted as a literal path) in this worktree.
  Evidence: `Resolve-Path : Cannot find path '...\\Scaffold\\-ProjectPath'` from `validate-milestone.ps1` invocation on 2026-03-10.

## Decision Log

- Decision: Split the feature into two modules: a pure C# core module and a Unity wrapper module.
  Rationale: The request requires non-Unity serializable classes and separate Unity wrappers; module split enforces this boundary at assembly level.
  Date/Author: 2026-03-10 / Codex

- Decision: Use global string identifiers for both definitions and instances, with one shared uniqueness gate.
  Rationale: The request states identifiers are globally unique and every entity can be discovered centrally.
  Date/Author: 2026-03-10 / Codex

- Decision: Use generic entity instances (`EntityInstance<TDefinition>`) to bind an instance directly to its definition type instead of linking through a definition id field.
  Rationale: The requested design change removes id-based instance-definition coupling and makes the relationship type-safe.
  Date/Author: 2026-03-10 / Codex

- Decision: Implement modifiers as an abstract base type with concrete subclasses (add, remove, multiply) instead of an enum switch.
  Rationale: The requested design change prefers behavior-per-type classes and keeps modifier logic open for extension.
  Date/Author: 2026-03-10 / Codex

- Decision: Store numeric attribute values as `double` in the core model for deterministic modifier math with minimal complexity.
  Rationale: The requested modifier examples are arithmetic (`+1 strength`, `x2 speed`), so numeric baseline is required.
  Date/Author: 2026-03-10 / Codex

## Outcomes & Retrospective

No outcomes yet. This section will summarize delivered behavior, remaining gaps, and lessons learned at each major milestone completion.
The entity system is now implemented with two modules: core runtime (`Scaffold.Entities`) and Unity wrappers (`Scaffold.Presentation.Entities`). Core behavior includes generic instance-definition linkage (`EntityInstance<TDefinition>`), abstract modifier behaviors (add/remove/multiply), attribute resolution with base-value fast path when no matching modifiers exist, and global id uniqueness via `EntityRegistry`.

Coverage and validation outcomes:

- New tests added for core behavior and wrapper conversions.
- `check-analyzers.ps1` result remains clean for in-scope code (only pre-existing AutoPacker findings remain).
- `run-editmode-tests.ps1` passed (`Total 49, Passed 49, Failed 0`).
- `validate-milestone.ps1` failed due pre-existing script argument handling (`-ProjectPath` resolution), not entity-module code.

Remaining gap:

- If required by process, fix `validate-milestone.ps1` separately so the combined gate script can execute successfully in this worktree.

## Context and Orientation

This repository is a modular Unity project with strict architecture boundaries and analyzer enforcement. New feature work must keep core logic Unity-free, declare dependencies explicitly via `.asmdef`, include tests for every module, and include module documentation under `Docs/`.

This plan introduces two new modules.

- Core module (no Unity references):
  - `Assets/Scripts/Core/Entities/Runtime/`
  - `Assets/Scripts/Core/Entities/Tests/`
  - `Assets/Scripts/Core/Entities/Samples/`
- Unity wrapper module (UnityEngine-dependent adapters only):
  - `Assets/Scripts/Presentation/Entities/Runtime/`
  - `Assets/Scripts/Presentation/Entities/Tests/`
  - `Assets/Scripts/Presentation/Entities/Samples/`

Documentation for these modules must be added at:

- `Docs/Core/Entities.md`
- `Docs/Presentation/Entities.md`

Definitions used in this plan:

- Entity Definition: the base recipe of an entity type.
- Entity Instance: one unique runtime/authoring instance tied to a definition through a generic type parameter plus modifiers.
- Attribute: key-value pair representing stats or values.
- Modifier: temporary attribute adjustment applied to an instance.
- Global Registry: central service used to register and retrieve definitions and instances by globally unique id.

## Plan of Work

Milestone 0 establishes exact contracts and creates modules with the repository workflow. Milestones 1 through 3 build the pure C# entity system. Milestone 4 adds Unity wrappers that contain core classes and expose conversion operators. Milestone 5 adds documentation and samples, then runs full milestone validation.

The implementation must remain additive and test-driven. Every milestone must run the quality loop defined by project rules: run edit mode tests, run analyzer checks, fix failures, rerun until clean, then commit.

## Interfaces and Dependencies

Create the following public core types in `Assets/Scripts/Core/Entities/Runtime/` under namespace `Scaffold.Entities`.

- `EntityAttribute`

      [Serializable]
      public sealed class EntityAttribute
      {
          public string Key;
          public double Value;
      }

- `EntityModifier`

      [Serializable]
      public abstract class EntityModifier
      {
          public string Id;
          public string TargetAttributeKey;
          public bool IsTemporary;

          public abstract double Apply(double currentValue);
      }

      [Serializable]
      public sealed class AddAttributeModifier : EntityModifier
      {
          public double Amount;

          public override double Apply(double currentValue);
      }

      [Serializable]
      public sealed class RemoveAttributeModifier : EntityModifier
      {
          public double Amount;

          public override double Apply(double currentValue);
      }

      [Serializable]
      public sealed class MultiplyAttributeModifier : EntityModifier
      {
          public double Factor;

          public override double Apply(double currentValue);
      }

- `EntityDefinition`

      [Serializable]
      public sealed class EntityDefinition
      {
          public string Id;
          public string DisplayName;
          public List<EntityAttribute> Attributes;
      }

- `EntityInstance`

      public interface IEntityInstance
      {
          string Id { get; }
          EntityDefinition Definition { get; }
          IReadOnlyList<EntityModifier> Modifiers { get; }

          bool TryGetAttributeValue(string key, out double value);
      }

      [Serializable]
      public sealed class EntityInstance<TDefinition> : IEntityInstance where TDefinition : EntityDefinition
      {
          public string Id;
          public TDefinition Definition;
          public List<EntityModifier> Modifiers;

          public bool TryGetAttributeValue(string key, out double value);
      }

- `EntityRegistry`

      public interface IEntityRegistry
      {
          bool RegisterDefinition(EntityDefinition definition);
          bool RegisterInstance(IEntityInstance instance);
          bool TryGetDefinition(string id, out EntityDefinition definition);
          bool TryGetInstance(string id, out IEntityInstance instance);
          bool UnregisterDefinition(string id);
          bool UnregisterInstance(string id);
          void Clear();
      }

      public sealed class EntityRegistry : IEntityRegistry

Registry behavior is strict:

- ids must be unique globally across both definitions and instances.
- registering a definition or instance with an existing id fails and returns `false`.
- registering an instance fails when its `Definition` is null.
- registering an instance fails when its attached definition id is not already registered.

Attribute resolution behavior in `EntityInstance<TDefinition>.TryGetAttributeValue(...)`:

- if no modifiers target the requested key, return the definition value directly with no new object allocation.
- if one or more modifiers target the key, apply matching modifier objects in deterministic list order by calling `Apply(currentValue)` on each.
- if attribute key does not exist on the definition, return `false`.

Create Unity wrapper types in `Assets/Scripts/Presentation/Entities/Runtime/` under namespace `Scaffold.Presentation.Entities`:

- `EntityAttributeAsset : ScriptableObject` containing one `EntityAttribute` field and conversion operators.
- `EntityModifierAsset : ScriptableObject` containing one `EntityModifier` field and conversion operators.
- `EntityDefinitionAsset : ScriptableObject` containing one `EntityDefinition` field and conversion operators.
- `EntityInstanceAsset : ScriptableObject` containing one `EntityInstance<EntityDefinition>` field and conversion operators.

Wrappers are data adapters only. No business logic, no registry ownership, and no modifier evaluation logic in wrapper classes.

Assembly dependency rules:

- `Scaffold.Entities` (core) has `noEngineReferences: true`.
- `Scaffold.Presentation.Entities` references `Scaffold.Entities` and Unity engine.
- test assemblies reference their target module and Unity Test Framework.

## Milestones

### Milestone 0: Module setup and contract lock

Complexity check: medium. Multiple new modules and assembly boundaries can drift if not locked first.

Mini plan:

1. Use the `/create-module` workflow (per repository rule) to scaffold `Core/Entities` and `Presentation/Entities` module layout.
2. Ensure `.asmdef` names and dependencies are correct before any runtime code is added.
3. Add placeholder tests that compile so validation scripts can run as soon as code lands.

Acceptance:

- both modules exist with Runtime/Tests/Samples folders and asmdefs.
- the core asmdef has no Unity engine references.
- validation scripts run successfully on baseline scaffolding.

### Milestone 1: Core entity models

Complexity check: low-to-medium. Model contracts are straightforward but must remain serializable and analyzer-compliant.

Implement `EntityAttribute`, abstract/concrete `EntityModifier` types, `EntityDefinition`, `IEntityInstance`, and `EntityInstance<TDefinition>` as plain serializable classes with simple fields and validation helpers (null/empty id guards).

Acceptance:

- core model classes compile and serialize cleanly.
- tests verify object creation, serialization compatibility assumptions, and simple construction invariants.

### Milestone 2: Attribute resolution and modifier engine

Complexity check: high. This milestone includes behavior and performance-sensitive no-allocation path.

Mini plan:

1. Implement definition attribute lookup by key.
2. Implement modifier targeting and deterministic polymorphic application through `EntityModifier.Apply(...)`.
3. Implement no-modifier fast path that returns base value directly without allocating new wrapper objects.
4. Add unit tests for add-only, remove-only, multiply-only, ordered combined modifiers, missing keys, and no-modifier path behavior.

Acceptance:

- querying unmodified `strength` returns base value from definition.
- querying modified `strength` returns value transformed by concrete modifier behavior.
- querying modified `speed` with multiply behaves as expected.
- tests explicitly cover requested scenario:
  - base `strength=5`, `AddAttributeModifier(+1)` => `6`
  - base `speed=3`, `MultiplyAttributeModifier(x2)` => `6`

### Milestone 3: Global registry and id uniqueness

Complexity check: medium-to-high. Correctness depends on cross-type uniqueness and lookup semantics.

Mini plan:

1. Implement `IEntityRegistry` and `EntityRegistry` with one uniqueness gate across all ids.
2. Add registration and lookup APIs for definitions and instances.
3. Enforce definition existence before instance registration by validating `instance.Definition.Id` is already registered.
4. Add tests for duplicate id rejection across types, happy-path retrieval, and unregister behavior.

Acceptance:

- definition lookup by id works.
- instance lookup by id works.
- same id cannot be reused for a definition and an instance.
- instance registration fails if its attached definition is null or unknown.

### Milestone 4: Unity wrappers (ScriptableObject adapters)

Complexity check: medium. Adapter design is simple but assembly boundaries must stay strict.

Implement ScriptableObject wrappers that each contain one core object and conversion operators. Keep wrappers thin and serialization-friendly.

Acceptance:

- wrappers compile in `Presentation` module only.
- conversion operators map data in both directions without business logic.
- wrapper tests verify data round-trip for each wrapper type.

### Milestone 5: Samples, docs, and integration validation

Complexity check: medium. Cross-module documentation and examples must remain consistent and runnable.

Add sample usage classes for both modules and author documentation files:

- `Docs/Core/Entities.md`
- `Docs/Presentation/Entities.md`

Docs must describe purpose, public API/interface, usage examples, and design decisions.

Acceptance:

- docs exist and reflect real code paths.
- samples compile and demonstrate registry + generic instance attribute fetch behavior.
- full validation scripts pass.

## Concrete Steps

Run all commands from `C:/Users/user/Documents/Unity/Scaffold`.

1. Scaffold modules (using repository workflow instruction):

      /create-module Core/Entities
      /create-module Presentation/Entities

2. Validate baseline after scaffolding:

      & ".\.agents\scripts\run-editmode-tests.ps1"
      & ".\.agents\scripts\check-analyzers.ps1"

3. Implement milestone code and tests incrementally, running the quality loop after each milestone:

      & ".\.agents\scripts\run-editmode-tests.ps1"
      & ".\.agents\scripts\check-analyzers.ps1"

4. Final milestone validation:

      & ".\.agents\scripts\validate-milestone.ps1"

Expected success signals:

- test script summary shows zero failed tests.
- analyzer script reports zero blockers/diagnostics.
- milestone validation script finishes successfully without failures.

## Validation and Acceptance

The full plan is accepted when all conditions are true:

- All core entity structures exist as plain serializable classes with no Unity dependency.
- Instance-definition linkage is generic (`EntityInstance<TDefinition>`) rather than id-field coupling.
- Modifiers are abstract behavior classes with concrete implementations (for example add/remove/multiply), not enum-switched operations.
- Unity ScriptableObject wrappers exist and only adapt to/from core models.
- Instance attribute fetch applies modifiers when present and returns base value directly when absent.
- Registry provides central definition/instance lookups and enforces global id uniqueness.
- EditMode tests and analyzer checks are clean.
- Module docs and samples exist and are up to date.

## Idempotence and Recovery

All validation commands are safe to rerun. If a milestone introduces failing tests or analyzer diagnostics, fix code and rerun scripts until clean before proceeding.

If module scaffolding is partially created, rerun workflow for missing pieces or manually align folder/asmdef names to the contract above, then rerun validation scripts.

No destructive migration is required for this feature.

## Artifacts and Notes

Implementation-time evidence to capture in this section:

- short analyzer script output proving zero diagnostics.
- short test script output proving pass counts.
- one sample transcript showing register definition, register instance, add modifier, query attributes.

Example expected scenario transcript:

    RegisterDefinition("orc_definition") => true
    RegisterInstance(EntityInstance<EntityDefinition>("orc_instance", definition: orc_definition)) => true
    TryGetAttributeValue("strength") with AddAttributeModifier(+1) => 6
    TryGetAttributeValue("speed") with MultiplyAttributeModifier(x2) => 6

## Revision Notes

- 2026-03-10 / Codex: Initial ExecPlan created from user requirements. Added module split, interfaces, milestones, validation loop, and architecture constraints so implementation can start without additional context.
- 2026-03-10 / Codex: Updated the design to use `EntityInstance<TDefinition>` generic linkage and abstract modifier subclasses (add/remove/multiply) per user request; revised contracts, milestone acceptance, and examples accordingly.
- 2026-03-10 / Codex: Implemented all milestones, updated living sections with outcomes and validation evidence, and recorded the `validate-milestone.ps1` script failure as an external blocker.
