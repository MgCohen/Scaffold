# Expand Scaffold Entities: definitions, instances, flyweight attributes

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository policy for ExecPlans is defined in `PLANS.md` at the repository root. This document must be maintained in accordance with that file.

## Purpose / Big Picture

After this work, a game can **author shared entity templates** as **definitions** (ScriptableObjects or serializable data), **spawn instances** that are always **bound to exactly one definition**, and **read attribute values** through instances using a **flyweight** split: shared identity and defaults live on the definition; **modifiers exist only on instances**. Each instance exposes a **stable unique identifier** for maps, networking, or save data. A **factory** creates instances from a definition without duplicating definition data. **Attribute** values are carried as small **records** holding a **string** payload (games can interpret as numbers, enums, or opaque text). **AttributeSO** is the Unity inspector-facing asset that wraps an `Attribute` and supports **implicit conversion** to `Attribute`. Instances can live as **MonoBehaviours** or as **plain serializable C# objects** by sharing one core data shape with minimal duplicated logic.

Someone can see it working when: unit tests (or a tiny sample type) construct a definition asset, create an instance via the factory, assert the instance’s definition reference and **instance id**, apply a **modifier only on the instance**, resolve the same logical attribute by **`AttributeSO`**, by **`Attribute`**, and by **string key**, and run `validate-changes.ps1` with a clean compilation gate.

## Progress

- [x] Author initial ExecPlan at `Plans/EntitiesExpand/EntitiesExpand-ExecPlan.md`.
- [ ] Milestone 1 — Core model: `Attribute` record (string payload), `AttributeSO`, implicit cast, definition base type, instance id type, and lookup contracts (by SO, record, string key). No MonoBehaviour yet beyond what is required for SO assets and drawers.
- [ ] Milestone 2 — Instance data core: serializable instance state (definition ref, id, modifier storage only on instance), attribute resolution path definition → instance (flyweight), factory API with generic hooks for subclasses.
- [ ] Milestone 3 — Unity hosts: MonoBehaviour adapter and/or serializable host with shared internals; property drawers for `Attribute` and `AttributeSO` fields; Editor assembly updates.
- [ ] Milestone 4 — Migration and cleanup: replace or adapt legacy `Entity` / `EntityAttribute` / float pipeline in `Assets/Packages/com.scaffold.entities/Runtime/Core/`; update `README.md`; add `Scaffold.Entities.Tests` coverage; run validation gate.
- [ ] Outcomes & Retrospective filled for completion.

## Surprises & Discoveries

Document unexpected behaviors, bugs, optimizations, or insights discovered during implementation. Provide concise evidence.

- Observation: (none yet)
  Evidence: (none yet)

## Decision Log

- Decision: **String-valued attributes** — The `Attribute` record’s payload is a **`string Value`** (and any minimal identity fields agreed in **Interfaces and Dependencies**). Games parse or interpret that string (float, int, bool, enum names) at use sites; the entities package does not embed a second numeric pipeline alongside strings in v1 of this expansion.
  Rationale: Matches the stakeholder request for string payloads and keeps the core type simple; avoids duplicating float math and string math in parallel until a clear need appears.
  Author: Stakeholder (ExecPlan author), initial draft.

- Decision: **Flyweight boundaries** — **Definition** objects hold default attribute entries (key → default `Attribute` / string value). **Instance** objects hold a reference to their definition, a unique **instance id**, and **only** runtime modifier lists (and any instance-local state explicitly called out in the plan). Instances do **not** copy full attribute tables from the definition; they resolve **effective** values by combining definition defaults with instance modifiers according to documented rules.
  Rationale: Matches “modifiers only exist on instances” and “read from definition through instances.”
  Author: Stakeholder (ExecPlan author), initial draft.

- Decision: **Subclassing via generics** — Public surface exposes generic bases such as `EntityDefinition<TSelf>` / `EntityInstance<TDefinition, TSelf>` (exact names to be finalized in code) so game code can derive **typed** definitions and instances without casting.
  Rationale: Stakeholder asked for child classes of definitions and instances with generic support.
  Author: Stakeholder (ExecPlan author), initial draft.

- Decision: **Unique instance id** — Use **`System.Guid`** as the canonical unique id, exposed via a small wrapper or readonly property (e.g. `InstanceId` struct wrapping `Guid`) so instances are easy to map in dictionaries and serialize deterministically when needed.
  Rationale: Stable, unique, and serialization-friendly without a central allocator.
  Author: Stakeholder (ExecPlan author), initial draft.

- Decision: **Dual hosting (MonoBehaviour vs serializable)** — Introduce a **single serializable core** type (e.g. `EntityInstanceState` or equivalent name) that holds definition binding, id, and modifiers. A **MonoBehaviour** host holds or references that core and forwards lifecycle if needed. Plain C# objects can embed the same core for deterministic simulation or server-side models.
  Rationale: Minimize duplicated logic while satisfying both hosting modes.
  Author: Stakeholder (ExecPlan author), initial draft.

- Decision: **Legacy API** — The current float-based `Entity`, `EntityAttribute`, `EntityAttributeEntry`, and `EntityAttributeModifierEntry` in `Assets/Packages/com.scaffold.entities/Runtime/Core/` are **superseded** by this model unless a thin adapter proves trivial. Prefer **one coherent model** in the package rather than two parallel attribute systems.
  Rationale: Avoid conflicting mental models and maintenance burden; this repo has no external consumers of the old API inside the tree (verify during implementation).
  Author: Stakeholder (ExecPlan author), initial draft.

## Outcomes & Retrospective

Summarize outcomes, gaps, and lessons learned at major milestones or at completion. Compare the result against the original purpose.

- (Pending implementation.)

## Context and Orientation

**Unity package root** for this feature is `Assets/Packages/com.scaffold.entities/`. Runtime code compiles under assembly **`Scaffold.Entities`** (`Assets/Packages/com.scaffold.entities/Runtime/Scaffold.Entities.asmdef`). Editor-only code compiles under **`Scaffold.Entities.Editor`**. Tests use **`Scaffold.Entities.Tests`** when present.

**Conceptual folders** (already started in this package): `Runtime/Core/` holds definition, instance, attributes, and factories; `Runtime/Behavior/` holds behavior runner code and should remain **unchanged** unless a type rename forces a mechanical update.

**Flyweight pattern** (here): **Intrinsic** (shared, immutable per archetype) data lives on the **definition**—attribute keys and default string values. **Extrinsic** (per-spawn) data lives on the **instance**—unique id, modifiers, and any per-instance overrides the plan allows. Reading an attribute always goes **through** an instance, which consults its definition for defaults and applies instance modifiers.

**Definition** means a serializable or `ScriptableObject` type that describes a kind of entity (unit type, item archetype). **Instance** means a runtime object representing one spawned entity bound to exactly one definition.

**Attribute** (new) means a small immutable **record** type holding a **string** value (and minimal fields needed for identity in code—see **Interfaces and Dependencies**). **AttributeSO** means a `ScriptableObject` that wraps or references that conceptual attribute for Unity assets and exposes an **implicit conversion** to `Attribute` where safe.

**Modifier** (instance-only) means a structured change applied on top of definition defaults (exact structure—additive string concat, keyed deltas, etc.—is fixed in Milestone 2 and recorded here when chosen).

**Factory** means the API that allocates a new instance (MonoBehaviour or plain object), assigns a fresh **instance id**, binds the **definition**, and initializes empty or default modifier collections.

## Plan of Work

Work proceeds in the milestones listed in **Progress**. Narratively: first land **types** that compile and are testable without Unity scenes (where possible): `Attribute` record, `AttributeSO`, definition/instance bases, id, and resolution rules. Second, implement **instance state** and **factory** with **generics** for subclassing. Third, add **Unity-specific** pieces: MonoBehaviour host, **property drawers** so **`AttributeSO` displays as an asset field** and serialized **`Attribute`** fields draw sensibly. Fourth, **remove or migrate** the legacy float `Entity` pipeline, update **package README**, add **tests**, and run the repository **validation script**.

Naming should stay under namespace **`Scaffold.Entities`** unless a sub-namespace (e.g. `Scaffold.Entities.Core`) improves clarity without fragmenting asmdefs.

Do **not** introduce new `#pragma warning disable` without explicit stakeholder approval in the same thread (repository rule).

## Concrete Steps

All commands assume a Windows environment with PowerShell. Repository root: the folder containing `PLANS.md` (this file lives at `Plans/EntitiesExpand/EntitiesExpand-ExecPlan.md`).

**During implementation after each milestone:**

1. From the repository root, run:

    powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests

   If automated tests exist for this package, omit `-SkipTests` when appropriate per `Docs/Testing.md`.

2. Expect **Compilation: PASS** and **Scripts asmdef audit: PASS** in the summary. If **Analyzers** reports non-zero **TOTAL**, follow `AGENTS.md` and `Docs/Testing.md` to distinguish blockers vs third-party noise.

3. Commit milestone changes with a message that states the milestone and the behavioral outcome.

**If Unity Editor is used for manual verification:** open the Scaffold project, let the asset database import, and confirm new ScriptableObjects and drawers compile without console errors.

## Validation and Acceptance

**Automated:** New tests under `Assets/Packages/com.scaffold.entities/Tests/` prove: (1) a definition can declare default attributes; (2) a factory-created instance references that definition and has a non-empty **unique id**; (3) modifiers can be added on the instance and **do not** mutate the definition’s defaults; (4) resolving by **`AttributeSO`**, by **`Attribute`**, and by **string key** returns consistent results for the same logical slot; (5) implicit conversion from **`AttributeSO`** to **`Attribute`** behaves as documented.

**Manual (Unity):** Create a test `AttributeSO` asset and a test `Definition` asset; add an instance host in a scene; verify inspector fields for **`AttributeSO`** and serialized **`Attribute`** render via drawers without raw YAML confusion.

**Gate:** `validate-changes.ps1` completes with the same success criteria as in **Concrete Steps**.

## Idempotence and Recovery

Re-running the validation script is safe. If a milestone introduces a breaking rename, keep a short migration note in **Decision Log** and prefer additive APIs until tests cover the new path, then remove legacy types in the same feature branch to avoid half-migrated states.

## Artifacts and Notes

Indented examples for a novice comparing success:

    Change Validation Summary
    ----------------------------
    Scripts asmdef audit: PASS (TOTAL:0)
    ...
    Compilation: PASS (exit code 0)
    ...
    Analyzers: PASS (TOTAL:0, BLOCKERS:0)

## Interfaces and Dependencies

Prescriptive targets (names may be adjusted in code if the **Decision Log** records the rename):

**`Attribute` record** — At minimum: a **string** property holding the attribute payload (name it `Value` unless a clash forces otherwise). If lookup-by-record requires a stable key, include a **string Key** or rely on a paired **`AttributeSO`** reference carried separately on definitions; the implementing milestone must choose one approach and document it here.

**`AttributeSO` : ScriptableObject** — Wraps or maps to the conceptual **`Attribute`** for editor and asset pipelines. Provides **`public static implicit operator Attribute(AttributeSO so)`** (or instance method) as agreed in implementation; if implicit conversion is ambiguous (nullability), document **explicit** fallback in **Decision Log**.

**Definition type** — `ScriptableObject`-based definition base class with generic self-type parameter for safe subclassing from games. Holds a collection of **default** attribute entries keyed by **`AttributeSO`** and/or string id.

**Instance type** — Core serializable state: **`InstanceId`** (wraps **`Guid`**), **reference to definition**, **modifier list** (no modifiers on definition). Methods to **get** effective attribute as **`Attribute`** after applying modifier rules.

**Factory** — Static or injectable service: `CreateInstance<TDefinition, TInstance>(TDefinition def, ...)` returning `TInstance` with new **Guid**, bound **definition**, and **empty** modifiers.

**Resolution API** on instance: `TryGetAttribute(AttributeSO, out Attribute)`, `TryGetAttribute(string key, out Attribute)`, and overload accepting **`Attribute`** as a key if the record carries enough identity; exact overload set to be finalized when **`Attribute`** shape is fixed.

**MonoBehaviour host** — Thin component referencing **`EntityInstanceState`** (or equivalent) or embedding it with **`[SerializeField]`**, exposing the same public surface as the plain serializable wrapper where practical.

**Editor** — `CustomPropertyDrawer` for **`Attribute`** and for fields that should show **`AttributeSO`** object fields in the inspector.

**Dependencies** — Unity engine only for runtime package; Editor assembly references **`UnityEditor`** and runtime assembly as today.

## Revision history

- **2026-04-05** — Initial ExecPlan authored from stakeholder requirements (definition/instance binding, flyweight, factory, id, generics, string attribute records, AttributeSO + drawers, dual hosting).
