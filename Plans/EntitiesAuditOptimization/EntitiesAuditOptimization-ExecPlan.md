# Entities package: audit follow-up (clarity, hot-path allocation, optional deeper optimization)

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository policy for ExecPlans is defined in `PLANS.md` at the repository root. This document must be maintained in accordance with that file.

Source review that motivates this plan is captured in `Plans/EntitiesAudit-Findings.md`. That file is background only; this ExecPlan repeats every instruction an implementer needs so work can proceed without opening the audit.


## Purpose / Big Picture

The audit concluded that `com.scaffold.entities` is structurally sound but has three practical gaps: documentation that no longer matches the shipped API, avoidable garbage-collection pressure when gameplay code repeatedly converts the same `AttributeSO` into an `Attribute` key on hot paths, and a non-obvious invariant inside `AttributeBag` that could bite future edits. After this work, package consumers read accurate docs, sample code demonstrates the recommended caching pattern (or a small first-party helper makes it trivial), and maintainers see the `AttributeBag` contract spelled out where it matters.

Someone can see it working when: the package `README.md` names the same public types and initialization patterns as the code under `Assets/Packages/com.scaffold.entities/`, at least one sample or test exercises attribute access using a cached `Attribute` key for a repeated `AttributeSO` reference, `.agents/scripts/validate-changes.sh` (or `.cmd` on Windows) completes successfully, and EditMode tests still pass when Unity is available.

Deeper optimizations (reusing effective `AttributeValue` instances, custom subscription storage, stable asset identity instead of name-based keys) stay out of scope until a later milestone explicitly adds profiling evidence or a separate identity design ExecPlan. This plan treats them as follow-ups, not mandatory deliverables.


## Progress

- [x] Author initial ExecPlan at `Plans/EntitiesAuditOptimization/EntitiesAuditOptimization-ExecPlan.md` (this file).
- [ ] Milestone 1 — Documentation alignment: update `Assets/Packages/com.scaffold.entities/README.md` (and any package XML or asmdef-only docs if they repeat the old names) so public type names, `InstanceId` construction, and entry points match `EntityComponent`, `EntityComponent<TDefinition>`, `EntityInstance<TDefinition>`, and `InstanceId` as implemented. No behavior change required.
- [ ] Milestone 2 — Hot-path key caching: eliminate repeated implicit `AttributeSO` → `Attribute` conversion in demonstrably hot usage. Preferred order: (a) add a short “Performance” subsection to the README describing the issue and showing a cached-key pattern; (b) update one representative sample under `Assets/Packages/com.scaffold.entities/Samples/` to cache `Attribute` (or a `readonly struct` wrapper if the team prefers) for slots read every frame; optionally (c) add a small documented helper on `AttributeSO` (for example a method or lazy-cached field established at edit time) only if it measurably reduces boilerplate without hiding costs—record the choice in the Decision Log.
- [ ] Milestone 3 — `AttributeBag` invariant visibility: add a concise remark on `RebuildCache()` and the split between serialized `entries` and runtime `localCache` (class-level or on the method) warning that calling `RebuildCache()` after runtime-only `Add()` calls drops those entries unless serialized state was updated. If a regression test can assert current intended behavior without flaking in Unity versions in use, add it; if not, document the manual verification step in `Validation and Acceptance`.
- [ ] Milestone 4 — Profile-gated optimizations (skip unless a profile or benchmark justifies them): (a) reduce allocations in `StringAttributeValue.Combine()` if string modifiers are hot; (b) reduce transient list allocation in `ClearModifiers()` if bulk clears are hot; (c) reduce subscription churn allocations if subscribe/unsubscribe happens at high frequency. Each item needs before/after evidence (Unity Profiler sample, simple allocation counter test, or micro-benchmark) recorded in `Surprises & Discoveries` before merging code.
- [ ] Milestone 5 — Optional spike (separate promotion criteria): stable runtime identity for attributes (for example a serialized GUID or asset instance id on `AttributeSO` used for dictionary keys instead of `name` + type). This is a design migration, not a quick fix; treat as its own decision with save/network compatibility notes. May remain unchecked until the product requests it.


## Surprises & Discoveries

- Observation: (none yet — fill in as milestones execute.)
  Evidence: …


## Decision Log

- Decision: This ExecPlan incorporates the audit’s recommendations but does not mandate high-risk architectural changes (struct keys, mutable effective values, flattening the bag chain) in the first delivery wave.
  Rationale: The audit ranks those as large trade-off moves; shipping clarity and low-risk allocation wins first matches “improve without harming readability.”
  Author: Planning session from `Plans/EntitiesAudit-Findings.md`, 2026-04-10.

- Decision: Name-based `Attribute` identity remains the default until Milestone 5 (or a successor plan) explicitly migrates to stable ids.
  Rationale: Renaming assets currently changes runtime keys; fixing that is a breaking or migration-sensitive change and deserves its own scope.
  Author: Planning session from audit, 2026-04-10.

- Decision: Milestone 4 items are conditional on profiling or measured allocation tests.
  Rationale: The audit warns that `StringBuilder`, list pooling, and custom notifier storage add complexity; they should not land on speculation alone.
  Author: Planning session from audit, 2026-04-10.


## Outcomes & Retrospective

- **Not started:** Summarize here after Milestone 1–3 (or beyond): what shipped, what was deferred, and whether samples/README/tests proved the caching guidance.


## Context and Orientation

The Unity package lives at `Assets/Packages/com.scaffold.entities/`. Runtime code is under `Runtime/Core/` (for example `EntityInstance.cs`, `EntityDefinition.cs`, `AttributeSO.cs`, `AttributeBag.cs`, notifier types, and `AttributeValue` subtypes). The MonoBehaviour entry type is `EntityComponent` and the generic variant is `EntityComponent<TDefinition>`, not the older `EntityBehaviour` name still mentioned in some docs.

An **`AttributeSO`** is a ScriptableObject asset that declares which logical attribute slot you mean and its value type. Gameplay code often holds references to these assets.

An **`Attribute`** is a small runtime key type (a record class in current code) built from an `AttributeSO`, typically carrying the asset’s name and type. Implicit conversion from `AttributeSO` to `Attribute` is convenient but allocates or reconstructs a key each time; the audit flags that as unnecessary cost when the same slot is queried every frame.

**`EntityDefinition`** holds authored default attribute entries shared by many entities. **`EntityInstance<TDefinition>`** holds per-spawn state: an `InstanceId`, the definition reference, modifiers, and internal bags. The audit describes a three-layer read path: an effective bag (cached results when modifiers apply), a runtime base bag for instance-added bases, and definition defaults. Recalculation must read from the base chain, not from stale effective values, so modifiers do not compound incorrectly.

**`AttributeBag`** keeps serialized `entries` plus a runtime `localCache`. **`RebuildCache()`** rebuilds from serialized entries. Runtime-only additions that do not update serialized `entries` can be lost if something calls `RebuildCache()` after those additions. Today that is acceptable only if `RebuildCache()` runs during setup, not during ordinary mutation; the plan makes that contract explicit.

**`InstanceId`** is a `record` wrapping an `int`; constructors like `new InstanceId(0)` or factory-assigned ids are used in code and samples. Documentation must not claim a static factory such as `InstanceId.New()` unless that API is added.


## Plan of Work

Milestone 1 is editorial. Read `README.md` side by side with `IEntity.cs`, `EntityComponent.cs`, `EntityComponentT.cs`, `EntityInstance.cs`, `InstanceId.cs`, and `EntityInstanceFactory.cs`. Replace outdated names and snippets so a new reader can copy-paste accurate initialization and attribute access examples.

Milestone 2 targets the audit’s highest-value, lowest-risk optimization: reuse the same `Attribute` instance (or an equivalent stable key obtained once per slot) when calling `TryGetAttribute`, `GetValue<T>`, or subscription APIs with the same `AttributeSO` in a loop. The README should explain why implicit conversion in a per-frame path is costly. Samples should not teach the anti-pattern in code that runs every `Update` unless the sample is explicitly labeled as cold-path only.

Milestone 3 is defensive documentation inside the codebase. The goal is to prevent a future contributor from calling `RebuildCache()` in the wrong lifecycle phase and silently dropping runtime-added slots. Prefer the smallest change that makes the hazard obvious without rewriting `AttributeBag` behavior.

Milestone 4 is optional and gated. Implement only when evidence shows a real bottleneck. Each change should preserve existing public semantics and be covered by tests where practical.

Milestone 5 is an optional spike for stable identity. If promoted, it will touch serialization, migration, and possibly saved games; keep it out of the default critical path until stakeholders accept migration cost.


## Concrete Steps

All commands assume repository root as working directory (`/workspace` or your local clone root).

1. After Milestone 1–3 code and doc edits, run the repository validation gate:

        .agents/scripts/validate-changes.sh

   On Windows shells that use the batch wrapper:

        .agents/scripts/validate-changes.cmd

2. When Unity is available, run EditMode tests for the package using the project’s documented Unity test command (see `Docs/Testing.md` if present). Expect the existing `EntityInstanceTests` (or successor) to pass; add a new test only when it proves a regression or a new helper’s contract.

3. For Milestone 4, capture a short profiler note or test output and paste a summary under `Surprises & Discoveries` so the Decision Log stays auditable.


## Validation and Acceptance

Milestone 1 succeeds when a reader can follow `README.md` without encountering `EntityBehaviour`, incorrect `InstanceId` APIs, or other symbols that do not exist in `Assets/Packages/com.scaffold.entities/`.

Milestone 2 succeeds when the README states the caching recommendation and the chosen sample (or test) shows non-allocating repeated access for at least one `AttributeSO` used in a loop or per-frame path. If Unity Profiler is unavailable, acceptance can be a code review checklist: implicit conversion not inside tight per-frame loops in first-party samples.

Milestone 3 succeeds when `AttributeBag` carries a clear warning on the `RebuildCache()` path (or equivalent class documentation), and `validate-changes` is clean.

Milestone 4 succeeds only when each merged optimization cites evidence in `Surprises & Discoveries` and tests or profiler steps still pass.

The full plan succeeds when Milestones 1–3 are complete, `validate-changes` is clean, and `Outcomes & Retrospective` records what was deferred to Milestones 4–5.


## Idempotence and Recovery

Documentation and comment-only milestones can be reapplied safely; prefer small commits per milestone. If `validate-changes` fails, fix analyzers or formatting before marking a milestone complete. Milestone 4 changes should be revertible by commit because they are performance-only; keep behavior-compatible tests green.


## Artifacts and Notes

Indented examples belong here as milestones complete (profiler screenshots described in text, short test output, or diff summaries).


## Interfaces and Dependencies

No new external dependencies are required. Optional Milestone 2 helper, if added, should live in `Assets/Packages/com.scaffold.entities/Runtime/Core/` next to `AttributeSO.cs` or `Attribute.cs`, respect existing analyzer rules (`AGENTS.md`, package Roslyn analyzers), and remain optional so existing call sites keep compiling.

---

Revision history: Initial version authored 2026-04-10 from `Plans/EntitiesAudit-Findings.md` to turn audit conclusions into executable milestones without assuming prior conversation context.
