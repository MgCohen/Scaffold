# Expand Scaffold Entities: definitions, instances, flyweight attributes

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository policy for ExecPlans is defined in `PLANS.md` at the repository root. This document must be maintained in accordance with that file.

## Purpose / Big Picture

After this work, a game can **author shared entity templates** as **definitions** (ScriptableObjects or serializable data), **spawn instances** that are always **bound to exactly one definition**, and **read attribute values** through instances using a **flyweight** split: shared identity and defaults live on the definition; **modifiers exist only on instances**. Each instance exposes a **stable unique identifier** for maps, networking, or save data. A **factory** creates instances from a definition without duplicating definition data. **Attribute** values are carried as small **records** holding a **string** payload (games can interpret as numbers, enums, or opaque text). **First-party identification** of which logical attribute you are reading or writing is always the **reference**—the **`Attribute` record instance** in memory and/or the **`AttributeSO`** asset reference—not the string. The string is **not** the primary “use site” key for first-party code. **Second-party** systems that **do not** hold a reference to the record must **fetch by string** by **iterating** the definition’s or instance’s attribute list and matching a string field (documented in **Interfaces and Dependencies**). For **performance**, APIs should **resolve by reference first** (direct lookup by **`AttributeSO`** or keyed by stable id derived from the record reference path), and use **string matching only as the slower fallback**. **AttributeSO** is the Unity inspector-facing asset that wraps an `Attribute` and supports **implicit conversion** to `Attribute`. Instances can live as **MonoBehaviours** or as **plain serializable C# objects** by sharing one core data shape with minimal duplicated logic.

Someone can see it working when: unit tests (or a tiny sample type) construct a definition asset, create an instance via the factory, assert the instance’s definition reference and **instance id**, apply a **modifier only on the instance**, resolve the same logical attribute **preferentially** by **`AttributeSO`** or **`Attribute`** reference, and **also** resolve equivalent data via **string** iteration for the no-reference case, then run `validate-changes.ps1` with a clean compilation gate.

## Progress

- [x] Author initial ExecPlan at `Plans/EntitiesExpand/EntitiesExpand-ExecPlan.md`.
- [x] Milestone 1 — Core model: `Attribute` struct (string payload + optional `MatchKey`), `AttributeSO`, implicit cast, `EntityDefinition`, `InstanceId`, lookup contracts (**reference-first**, string scan secondary). Implemented in `Assets/Packages/com.scaffold.entities/Runtime/Core/`.
- [x] Milestone 2 — `EntityInstanceState`, `EntityModifierEntry`, `EntityInstanceFactory`, flyweight resolution, `EntityInstance<TDefinition>` typed subclass.
- [x] Milestone 3 — `Entity` MonoBehaviour host, `AttributePropertyDrawer`, legacy `EntityAttributeEntryPropertyDrawer` removed.
- [x] Milestone 4 — Legacy float `Entity` / `EntityAttribute` / entry types removed; `README.md` updated; `EntityInstanceStateTests` added; `validate-changes.ps1 -SkipTests` clean; EditMode suite pass (68/68).
- [x] Outcomes & Retrospective updated below.

## Surprises & Discoveries

Document unexpected behaviors, bugs, optimizations, or insights discovered during implementation. Provide concise evidence.

- Observation: Roslyn **SCA2001** / **SCA2003** required careful member and method order on `EntityInstanceState` (public `TryGetAttribute` overloads could not call a later overload without tripping call-order rules; string scan path uses `GetEffectiveAttribute` directly). Private helpers were split to satisfy **SCA2003** line limits.
  Evidence: Analyzer failures until `TryFindAttributeByStringScan` / `TryMatchSlotByString` extraction; `validate-changes.ps1` **TOTAL:0** after fixes.

- Observation: **SCA1007** flagged `else if` in `Assets/Packages/com.scaffold.states/Runtime/Store.cs` because the `else` branch was not a braced block starting on the next line; wrapped `else { if (...) { } }` to satisfy the gate (same validation run as entities work).
  Evidence: Analyzer output listed `Store.cs` until brace fix; then **TOTAL:0**.

## Decision Log

- Decision: **String field vs identity** — The `Attribute` record includes a **string** field used as **payload** (and optionally as a **secondary lookup key** for systems without a reference—see below). It is **not** the first-party identifier at call sites. **First-party** code identifies attributes by **reference**: the **`Attribute` record** in hand and/or **`AttributeSO`**. **Second-party** code that lacks a reference resolves by **string** by **iterating** the stored list of attribute entries and comparing the string field (or a dedicated key field if split from payload—record in **Decision Log** when chosen). Resolution APIs **must** prefer **reference-based** lookups (**`AttributeSO`**, **`Attribute`**) and treat **string** resolution as the slower path.
  Rationale: Matches stakeholder clarification: string is not the use-site identity; references are authoritative; string is for foreign systems and fallback matching.
  Author: Stakeholder (ExecPlan author), 2026-04-05 revision.

- Decision: **String-valued payload** — Games parse or interpret the string payload (float, int, bool, enum names) where they consume **`Attribute`** values; the entities package does not embed a second numeric pipeline alongside strings in v1 of this expansion.
  Rationale: Keeps the core type simple; avoids duplicating float math and string math in parallel until a clear need appears.
  Author: Stakeholder (ExecPlan author), initial draft; split from identity decision 2026-04-05.

- Decision: **Flyweight boundaries** — **Definition** objects hold default attribute entries keyed by **`AttributeSO`** (and thus by first-party reference), each with a default **`Attribute`** payload. **Instance** objects hold a reference to their definition, a unique **instance id**, and **only** runtime modifier lists (and any instance-local state explicitly called out in the plan). Instances do **not** copy full attribute tables from the definition; they resolve **effective** values by combining definition defaults with instance modifiers according to documented rules.
  Rationale: Matches “modifiers only exist on instances” and “read from definition through instances.”
  Author: Stakeholder (ExecPlan author), initial draft.

- Decision: **Subclassing via generics** — Public surface exposes generic bases such as `EntityDefinition<TSelf>` / `EntityInstance<TDefinition, TSelf>` (exact names to be finalized in code) so game code can derive **typed** definitions and instances without casting.
  Rationale: Stakeholder asked for child classes of definitions and instances with generic support.
  Author: Stakeholder (ExecPlan author), initial draft.

- Decision: **Implementation naming (2026-04-05)** — Shipped `EntityInstance<TDefinition> : Entity where TDefinition : EntityDefinition` for typed instance access. **Non-generic** `EntityDefinition : ScriptableObject` (Unity-friendly) instead of a generic `ScriptableObject` definition type; games subclass `EntityDefinition` concretely per archetype.
  Rationale: Unity serialization and asset workflows favor concrete `ScriptableObject` subclasses; generic instance wrapper covers the typed-instance requirement without generic SO complexity.
  Author: Implementation pass (ExecPlan maintainer), 2026-04-05.

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

- **Achieved:** Definition-bound `EntityInstanceState` with `InstanceId`, `EntityDefinition` defaults keyed by `AttributeSO`, instance-only `EntityModifierEntry` list, `TryGetAttribute` overloads (`AttributeSO`, `Attribute` with `MatchKey`, string scan), `AttributeSO` implicit conversion to `Attribute`, `Entity` MonoBehaviour host + `EntityInstanceFactory`, `EntityInstance<TDefinition>`, Editor drawer for `Attribute`, EditMode tests, package README refresh, legacy float attribute API removed.
- **Gaps / follow-ups:** Optional events/callbacks on attribute value change (not in v1). Behavior runner unchanged; games migrate to new `Entity` surface. Modifier combination uses float-sum when all segments parse, else string concat—documented in README.

## Context and Orientation

**Unity package root** for this feature is `Assets/Packages/com.scaffold.entities/`. Runtime code compiles under assembly **`Scaffold.Entities`** (`Assets/Packages/com.scaffold.entities/Runtime/Scaffold.Entities.asmdef`). Editor-only code compiles under **`Scaffold.Entities.Editor`**. Tests use **`Scaffold.Entities.Tests`** when present.

**Conceptual folders** (already started in this package): `Runtime/Core/` holds definition, instance, attributes, and factories; `Runtime/Behavior/` holds behavior runner code and should remain **unchanged** unless a type rename forces a mechanical update.

**Flyweight pattern** (here): **Intrinsic** (shared, immutable per archetype) data lives on the **definition**—which **`AttributeSO`** / logical slot each default belongs to, and the default **string payload**. **Extrinsic** (per-spawn) data lives on the **instance**—unique id, modifiers, and any per-instance overrides the plan allows. Reading an attribute always goes **through** an instance, which consults its definition for defaults and applies instance modifiers. **Identity** of which attribute is which, for first-party code, is by **reference** (`AttributeSO` or the **`Attribute` record** in collections), not by treating the string payload as the primary key at the call site.

**Definition** means a serializable or `ScriptableObject` type that describes a kind of entity (unit type, item archetype). **Instance** means a runtime object representing one spawned entity bound to exactly one definition.

**Attribute** (new) means a small immutable **record** type holding a **string** payload (and optional separate **lookup key** string if the design splits “display/payload” from “match key”—see **Interfaces and Dependencies**). **AttributeSO** means a `ScriptableObject` that wraps or references that conceptual attribute for Unity assets and exposes an **implicit conversion** to `Attribute` where safe.

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

**Automated:** New tests under `Assets/Packages/com.scaffold.entities/Tests/` prove: (1) a definition can declare default attributes; (2) a factory-created instance references that definition and has a non-empty **unique id**; (3) modifiers can be added on the instance and **do not** mutate the definition’s defaults; (4) resolving by **`AttributeSO`** and by **`Attribute` reference** returns the expected effective value; (5) resolving by **string** (iterative match, no reference) returns the **same** effective value for the same logical slot; (6) implicit conversion from **`AttributeSO`** to **`Attribute`** behaves as documented. Where practical, assert that **reference-first** code paths do not perform unnecessary full-list scans (implementation may use counters or test doubles to verify).

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

**`Attribute` record** — Holds a **string** payload (e.g. **`Value`** or **`Payload`**) for game interpretation. **Optional:** a separate **string** used only for **second-party** matching (e.g. **`Key`** or reuse of a dedicated field) if payload and lookup token must differ; if a single string serves both, document that **first-party code still does not use that string as the primary selector**—selection is by **reference**. The record type itself is the **first-party** handle when you already hold it in memory.

**`AttributeSO` : ScriptableObject** — The **authoritative first-party identity** in asset and inspector workflows: definitions and modifiers reference **`AttributeSO`** instances. Wraps or maps to the conceptual **`Attribute`** for serialization. Provides **`public static implicit operator Attribute(AttributeSO so)`** (or instance method) as agreed in implementation; if implicit conversion is ambiguous (nullability), document **explicit** fallback in **Decision Log**.

**Definition type** — `ScriptableObject`-based definition base class with generic self-type parameter for safe subclassing from games. Holds a collection of **default** attribute entries; **primary** association is **`AttributeSO` → default `Attribute`** (or index built from **`AttributeSO`** references). Internal storage may also allow **string**-based **scan** APIs for second-party use.

**Instance type** — Core serializable state: **`InstanceId`** (wraps **`Guid`**), **reference to definition**, **modifier list** (no modifiers on definition). Methods to **get** effective attribute as **`Attribute`** after applying modifier rules.

**Factory** — Static or injectable service: `CreateInstance<TDefinition, TInstance>(TDefinition def, ...)` returning `TInstance` with new **Guid**, bound **definition**, and **empty** modifiers.

**Resolution API** on instance (order matters for contract and performance):

  - **Preferred:** `TryGetAttribute(AttributeSO key, out Attribute value)` — direct lookup (dictionary or keyed list) keyed by the **asset reference**.
  - **Preferred when the caller holds the record:** overloads that accept an **`Attribute`** “template” or key record if the storage model supports stable correlation (exact signature depends on whether instances store modifiers keyed by **`AttributeSO`** only).
  - **Fallback (second-party):** `TryGetAttribute(string match, out Attribute value)` — **iterates** the effective attribute list (or definition defaults + instance overlay) and compares against the string field used for matching (payload or dedicated key—see **`Attribute`** record decision). Document **O(n)** behavior; do not present this as the default path for hot code.

Implementation note: **Never** imply that the string is the primary key for **first-party** gameplay code; document in public API remarks that **`AttributeSO`** / reference paths are **optimized** and string lookup is for **interop** or **late-bound** scenarios.

**MonoBehaviour host** — Thin component referencing **`EntityInstanceState`** (or equivalent) or embedding it with **`[SerializeField]`**, exposing the same public surface as the plain serializable wrapper where practical.

**Editor** — `CustomPropertyDrawer` for **`Attribute`** and for fields that should show **`AttributeSO`** object fields in the inspector.

**Dependencies** — Unity engine only for runtime package; Editor assembly references **`UnityEditor`** and runtime assembly as today.

## Revision history

- **2026-04-05** — Implementation pass: milestones 1–4 delivered in `com.scaffold.entities`; ExecPlan **Progress**, **Surprises**, **Decision Log** (implementation naming), **Outcomes** updated.
- **2026-04-05** — Stakeholder clarification: **first-party** identification is **reference** (`Attribute` / **`AttributeSO`**); **string** is **second-party** (iterate to match) when no reference exists; APIs **prefer reference-first** resolution, string as slower fallback.
- **2026-04-05** — Initial ExecPlan authored from stakeholder requirements (definition/instance binding, flyweight, factory, id, generics, string attribute records, AttributeSO + drawers, dual hosting).
