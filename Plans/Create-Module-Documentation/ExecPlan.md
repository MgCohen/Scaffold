# Create and Standardize Module Documentation for Architecture Modules

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

After this work, every module listed in the `Architecture.md` "Modules" section will have a complete, consistent document in `Docs/`, using a shared structure that helps contributors understand intent, API surface, architecture, internal behaviors, usage, tests, and related modules. This removes documentation gaps and makes onboarding and maintenance faster because all module docs follow the same navigation model and level of detail.

The user-visible outcome is nine module documentation files in `Docs/`:

- `Docs/Core/MVVM.md`
- `Docs/Infra/Containers.md`
- `Docs/Infra/Events.md`
- `Docs/Infra/Navigation.md`
- `Docs/Infra/NetworkMessages.md`
- `Docs/Generators/AutoPacker.md` (refactor existing file)
- `Docs/Tools/Maps.md`
- `Docs/Tools/Records.md`
- `Docs/Tools/Types.md`

## Progress

- [x] (2026-03-09 00:00Z) Created `Plans/Create-Module-Documentation/ExecPlan.md` with full execution scope and milestones.
- [x] (2026-03-09 17:52Z) Run Milestone 0 (template and validation contract).
- [x] (2026-03-09 18:05Z) Run Milestone 1 (MVVM planning + drafting).
- [x] (2026-03-09 18:32Z) Refined the shared template contract after MVVM feedback (API-first usage, architecture section, Internal Services section, and dependency graph requirement).
- [x] (2026-03-09 18:44Z) Run Milestone 2 (Containers planning + drafting).
- [x] (2026-03-09 18:56Z) Run Milestone 3 (Events planning + drafting).
- [x] (2026-03-09 19:10Z) Run Milestone 4 (Navigation planning + drafting).
- [x] (2026-03-09 19:24Z) Expanded `Docs/Infra/Navigation.md` with stack/open-close-return behavior, lifecycle timing, view callbacks, `ViewConfig`, and transition handler details.
- [x] (2026-03-09 19:39Z) Run Milestone 5 (NetworkMessages planning + drafting).
- [x] (2026-03-09 20:02Z) Run Milestone 6 (AutoPacker planning + drafting refactor).
- [x] (2026-03-09 20:03Z) Run Milestone 7 (Maps planning + drafting).
- [x] (2026-03-09 20:04Z) Run Milestone 8 (Records planning + drafting).
- [x] (2026-03-09 20:06Z) Run Milestone 9 (Types planning + drafting).
- [x] (2026-03-09 20:09Z) Run Milestone 10 (cross-doc consolidation and final validation).

## Surprises & Discoveries

- Observation: A single "How to use" section can become too implementation-heavy when internal subsystems (like binding pipelines) are mixed with consumer workflow guidance.
  Evidence: MVVM draft feedback requested moving internal behavior details out of usage and centering usage on `View`, `ViewModel`, and `Bind()` public flows.

- Observation: MVVMInstaller is currently an empty installer stub, so MVVM runtime behavior is driven primarily by runtime classes and direct binding usage rather than container registration in this module.
  Evidence: `Assets/Scripts/Core/MVVM/Container/MVVMInstaller.cs` defines `Install(...)` with an empty body.

- Observation: Containers runtime is intentionally designed around abstraction boundaries (`IContainerRegistry`, `IContainerResolver`) while adapter implementations remain internal, which keeps feature code DI-provider-agnostic.
  Evidence: Public contracts live in `Runtime/Contracts`, while VContainer-specific adapters are `internal` in `Runtime/Implementation`.

- Observation: Events uses a dual-map strategy (typed delegate lookup + event-type dispatch map) to support both ergonomic typed APIs and correct unsubscribe behavior.
  Evidence: `EventController` maintains `eventLookups` and `events` dictionaries and bridges typed handlers into `Action<ContextEvent>` delegates.

- Observation: Navigation behavior is split into multiple focused internals (stack, provider, middleware, transitions), with `NavigationController` acting as orchestration glue rather than a monolithic implementation.
  Evidence: `NavigationController` composes `NavigationStack`, `NavigationProvider`, `NavigationTransitions`, and `NavigationMiddleware` in its constructor.

- Observation: NetworkMessages keeps the public API compact while hiding most complexity in handler mapping and serialization wrapping internals.
  Evidence: The public contract is a small `INetworkMessageDispatcher` interface, while implementation handles named routing, writer creation, and wrapper conversion.

- Observation: Even very small modules (like Records) benefit from the full template because constraints and intent become explicit instead of implied.
  Evidence: `Docs/Tools/Records.md` now clearly documents the `IsExternalInit` shim purpose, usage, and test behavior despite minimal runtime code.

## Decision Log

- Decision: Scope is limited to the nine modules named in the `Architecture.md` "Modules" section (MVVM, Containers, Events, Navigation, NetworkMessages, AutoPacker, Maps, Records, Types).
  Rationale: The request explicitly targets the module list in that section.
  Date/Author: 2026-03-09 / planning

- Decision: Documentation path strategy is flat `Docs/` root, not `Docs/Core|Infra|Tools` nesting.
  Rationale: User preference selected for this initiative.
  Date/Author: 2026-03-09 / planning

- Decision: `Docs/Generators/AutoPacker.md` will be fully refactored and treated as a normal milestone.
  Rationale: User requested AutoPacker to be used as an initial reference while still being refreshed to match the shared template.
  Date/Author: 2026-03-09 / planning

- Decision: Milestone 0 is satisfied by defining one canonical doc template and one reusable validation contract directly inside this ExecPlan.
  Rationale: This gives every later milestone a fixed drafting target and fixed acceptance gate before cross-doc consolidation.
  Date/Author: 2026-03-09 / implementation

- Decision: Document MVVM test execution primarily through Unity EditMode Test Runner plus a CLI batch-mode example, instead of `dotnet test` for the root csproj.
  Rationale: The module test assembly is Unity-oriented (`Scaffold.MVVM.Tests.asmdef`) and project guidance centers on Unity Test Runner workflows.
  Date/Author: 2026-03-09 / implementation

- Decision: Replace `How to expand` with `Architecture and key behaviors` plus `Internal Services`, and require `How to use` to remain API-first.
  Rationale: This separates consumer guidance from internal implementation details while still documenting key internal systems clearly.
  Date/Author: 2026-03-09 / implementation

- Decision: `Docs/Infra/Containers.md` documents `Bootstrap` in public API because it is a key consumer entrypoint, while keeping adapter internals in `Internal Services`.
  Rationale: Consumers interact with startup composition through `Bootstrap`, but should not couple to `VContainer*` types.
  Date/Author: 2026-03-09 / implementation

- Decision: `Docs/Infra/Events.md` treats `EventController` as the primary usage entry while still documenting `IEventBus` as the stable abstraction boundary.
  Rationale: Samples/tests instantiate `EventController` directly, but architectural coupling should target `IEventBus` where possible.
  Date/Author: 2026-03-09 / implementation

- Decision: `Docs/Infra/Navigation.md` documents transition/middleware/stack subsystems in `Internal Services` and keeps `How to use` centered on `INavigation` and `IViewController` contracts.
  Rationale: Consumers primarily interact with `INavigation`, while internals are essential for architecture understanding but secondary for day-to-day usage.
  Date/Author: 2026-03-09 / implementation

- Decision: `Docs/Infra/NetworkMessages.md` emphasizes unmanaged payload constraints and lifecycle cleanup (`UnregisterHandler` + `Dispose`) in usage flow.
  Rationale: These constraints directly affect correctness and are easy to miss when only looking at handler registration examples.
  Date/Author: 2026-03-09 / implementation

- Decision: Milestone 10 consolidation validates all nine target docs with one automated heading pass and related-section sanity checks before marking plan complete.
  Rationale: A single objective pass reduced manual drift and confirmed template consistency across the full module set.
  Date/Author: 2026-03-09 / implementation

## Outcomes & Retrospective

Milestones 1-10 outcome: all 9 target module docs (`MVVM`, `Containers`, `Events`, `Navigation`, `NetworkMessages`, `AutoPacker`, `Maps`, `Records`, `Types`) now follow the upgraded template with Mermaid dependency graphs, architecture snippets per key point, API-first usage guidance, dedicated `Internal Services`, and one-line public API descriptions. Consolidation validation passed with no missing required headings across the target set.

## Context and Orientation

This repository is a modular Unity project where each module has isolated runtime, samples, and tests, usually with dedicated `.asmdef` and root `.csproj` entries. Existing module documentation coverage is incomplete: only `Docs/Generators/AutoPacker.md` existed before this plan.

The module sources to inspect are:

- `Assets/Scripts/Core/MVVM/`
- `Assets/Scripts/Infra/Containers/`
- `Assets/Scripts/Infra/Events/`
- `Assets/Scripts/Infra/Navigation/`
- `Assets/Scripts/Infra/NetworkMessages/`
- `Generators/AutoPacker/`
- `Assets/Scripts/Tools/Maps/`
- `Assets/Scripts/Tools/Records/`
- `Assets/Scripts/Tools/Types/`

Each module document must include these required sections (exact headings):

- `Summary`
- `Bird's Eye View`
- `Architecture and key behaviors`
- `How to use`
- `Internal Services`
- `Public api`
- `How to test`
- `Related docs and modules`

## Template Contract (Milestone 0 Output)

Use this exact section order for every module document in `Docs/`:

1. `## Summary`
2. `## Bird's Eye View`
3. `## Architecture and key behaviors`
4. `## How to use`
5. `## Internal Services`
6. `## Public api`
7. `## How to test`
8. `## Related docs and modules`

Minimum content contract by section:

- `Summary`: lead with module purpose and user-visible effects first, then implementation details.
- `Bird's Eye View`: module folder orientation plus a visual dependency graph (Mermaid preferred) covering external and internal dependencies.
- `Architecture and key behaviors`: explain key classes/flows (including internal behaviors), and include at least one compact snippet for each key point.
- `How to use`: focus on public APIs and common consumer workflow, not internal service implementation.
- `Internal Services`: dedicated notes for internal subsystems (for example binding or event internals) so they do not dominate the usage section.
- `Public api`: list key public interfaces/classes/attributes with source file paths and a one-line explanation per item.
- `How to test`: exact runnable command(s), where to run them, and what success looks like.
- `Related docs and modules`: at least two related references (other module docs and/or architecture/plans docs).

Reusable acceptance gate for every drafting milestone:

1. Target doc file exists at expected `Docs/*.md` path.
2. All required headings exist exactly as written above.
3. Architecture and key behaviors contains per-key-point snippets.
4. Public api contains real symbols that exist in module source files and each item has a one-line explanation.
5. `How to use` is API-first and does not focus primarily on internal behaviors.
6. `How to test` contains executable commands plus expected pass behavior.
7. `Related docs and modules` contains at least two relevant links/references.

## Plan of Work

The implementation proceeds module by module, and each module has two phases that must be completed in order.

Phase A is the Planning phase. In this phase, inspect module runtime contracts, implementation entry points, samples, tests, and assembly definitions. Identify the public API candidates (interfaces, base classes, entry methods, attributes, and service installers), collect real usage examples from `Samples`, and collect concrete test commands from module test assemblies and available project test runners.

Phase B is the Drafting phase. In this phase, write or refactor the corresponding `Docs/*.md` file using the required section headings. The doc must explain module purpose in plain language, include a concise bird-eye architecture narrative grounded in actual folders and key types plus visual dependency graph context (Mermaid preferred), include an architecture section for key behaviors (including internal subsystems), provide concrete usage focused on public APIs, list public API symbols with one-line descriptions and file references, specify test execution paths and expected outcomes, and link related modules/docs.

After all nine module milestones complete, run a consolidation pass to normalize section names, writing style, and cross-links across all documents.

## Milestones

### Milestone 0: Shared Template and Acceptance Contract

Define the shared document skeleton once, including section ordering, expected depth per section, and minimum acceptance criteria for each section. Create a repeatable validation checklist that is reused for all module milestones.

Acceptance for Milestone 0 is a written template contract in this plan and a documented validation routine ready to run after each module draft.

### Milestone 1: MVVM (Planning then Drafting)

Planning: inspect `Assets/Scripts/Core/MVVM/Runtime`, `Samples`, `Tests`, and container installer integration. Identify binding contracts (`IBind*` family), view/viewmodel contracts, and core implementation classes (`ViewModel`, `View`, `BindedProperty`, `BindedCollection`, etc.).

Drafting: author `Docs/Core/MVVM.md` with required sections, concrete usage centered on `View`, `ViewModel`, and `Bind()` utilities, architecture/internal behavior sections for Binding and ViewEvents, public API list from contracts with one-liners, test instructions from `Tests/MVVMTests.cs`, and links to related modules (`Navigation`, `Events`, `Containers`).

### Milestone 2: Containers (Planning then Drafting)

Planning: inspect `Assets/Scripts/Infra/Containers/Runtime` contracts and implementations, container adapter pattern, installers, and use cases.

Drafting: author `Docs/Infra/Containers.md` with required sections, showing registration/resolution flow, architecture/internal behaviors, public API list from `IContainer*` contracts and `Installer` with one-liners, and test guidance from `Tests/ContainersTests.cs`.

### Milestone 3: Events (Planning then Drafting)

Planning: inspect event contracts (`ContextEvent`, `IEventBus`), controller implementation, installer, and sample use cases.

Drafting: author `Docs/Infra/Events.md` with required sections, event publication/subscription usage, architecture/internal behavior coverage, public API list with one-liners, and test guidance from `Tests/EventsTests.cs`.

### Milestone 4: Navigation (Planning then Drafting)

Planning: inspect navigation contracts, enums, controller/stack/transitions implementation, middleware interfaces, settings assets, and installer.

Drafting: author `Docs/Infra/Navigation.md` with required sections, navigation lifecycle usage, architecture/internal behavior coverage, public API list with one-liners, and test guidance from `Tests/NavigationTests.cs`.

### Milestone 5: NetworkMessages (Planning then Drafting)

Planning: inspect dispatcher contract/implementation, model wrappers, sample message flows, and tests.

Drafting: author `Docs/Infra/NetworkMessages.md` with required sections, dispatch usage examples, architecture/internal behavior coverage, public API list with one-liners, and test guidance from `Tests/NetworkMessagesTests.cs`.

### Milestone 6: AutoPacker (Planning then Drafting Refactor)

Planning: inspect `Generators/AutoPacker/src` for contracts and generator internals, reconcile with current `Docs/Generators/AutoPacker.md`, and identify outdated statements.

Drafting: refactor `Docs/Generators/AutoPacker.md` into the shared section template while preserving useful existing examples. Ensure section names, architecture/internal behavior coverage, and testing instructions match this plan and include generator build/publish workflow.

### Milestone 7: Maps (Planning then Drafting)

Planning: inspect map/index runtime classes, samples, and tests for key patterns (`Map`, `Indexer`, and index flavors).

Drafting: author `Docs/Tools/Maps.md` with required sections, map/index usage examples, architecture/internal behavior coverage, public API list with one-liners, and test guidance from `Tests/MapIndexerTests.cs`.

### Milestone 8: Records (Planning then Drafting)

Planning: inspect module scope centered on record compatibility shim and sample/tests.

Drafting: author `Docs/Tools/Records.md` with required sections, clear module purpose/effects first, architecture/internal behavior coverage, usage example from `Samples/RecordsUseCases.cs`, public API list with one-liners, and test guidance from `Tests/RecordsTests.cs`.

### Milestone 9: Types (Planning then Drafting)

Planning: inspect runtime contracts/implementation, editor tooling, attributes, sample use cases, and tests.

Drafting: author `Docs/Tools/Types.md` with required sections, runtime/editor boundaries, architecture/internal behavior coverage, public API list with one-liners, and test guidance from `Tests/TypesTests.cs`.

### Milestone 10: Consolidation and Final Validation

Run a cross-document QA pass for consistency and completeness: normalize terminology, verify all required headings exist in all nine docs, ensure related-links are non-empty and accurate, ensure every `Public api` section references real symbols/files with one-line descriptions, and verify every `How to test` section includes executable commands and expected outcomes.

Record completion evidence in `Progress`, `Surprises & Discoveries`, and `Outcomes & Retrospective`.

## Concrete Steps

Run commands from repository root `C:/Users/user/Documents/Unity/Scaffold`.

1. Inspect each module source, samples, tests, and assembly definitions before writing its doc.

    Get-ChildItem -Recurse -File Assets/Scripts/Core/MVVM -Include *.cs,*.asmdef
    Get-ChildItem -Recurse -File Assets/Scripts/Infra/Containers -Include *.cs,*.asmdef
    Get-ChildItem -Recurse -File Assets/Scripts/Infra/Events -Include *.cs,*.asmdef
    Get-ChildItem -Recurse -File Assets/Scripts/Infra/Navigation -Include *.cs,*.asmdef
    Get-ChildItem -Recurse -File Assets/Scripts/Infra/NetworkMessages -Include *.cs,*.asmdef
    Get-ChildItem -Recurse -File Assets/Scripts/Tools/Maps -Include *.cs,*.asmdef
    Get-ChildItem -Recurse -File Assets/Scripts/Tools/Records -Include *.cs,*.asmdef
    Get-ChildItem -Recurse -File Assets/Scripts/Tools/Types -Include *.cs,*.asmdef
    Get-ChildItem -Recurse -File Generators/AutoPacker -Include *.cs,*.csproj

2. Draft or refactor each target doc in `Docs/`.

3. Run structural validation for section headings.

    $docs = @('Docs/Core/MVVM.md','Docs/Infra/Containers.md','Docs/Infra/Events.md','Docs/Infra/Navigation.md','Docs/Infra/NetworkMessages.md','Docs/Generators/AutoPacker.md','Docs/Tools/Maps.md','Docs/Tools/Records.md','Docs/Tools/Types.md')
    $required = @('## Summary','## Bird''s Eye View','## Architecture and key behaviors','## How to use','## Internal Services','## Public api','## How to test','## Related docs and modules')
    foreach($doc in $docs){ foreach($h in $required){ if(-not (Select-String -Path $doc -Pattern [regex]::Escape($h) -Quiet)){ Write-Output "Missing '$h' in $doc" } } }

4. Run content validation pass for public APIs and related links.

    foreach($doc in $docs){ Select-String -Path $doc -Pattern '^## Public api|^## Related docs and modules|^## How to use|^## Internal Services|^## Architecture and key behaviors' }

5. Update this ExecPlan's living sections with actual implementation notes and evidence snippets.

## Validation and Acceptance

This plan is complete when all of the following are true:

1. Exactly nine module docs exist at the target `Docs/*.md` paths listed in `Purpose / Big Picture`.
2. Every module doc includes all required sections with exact section names.
3. Every `Public api` section lists real module symbols, their source locations, and one-line descriptions.
4. Every `How to use` section focuses on public API workflow (not internal details as the primary focus).
5. Every `How to test` section provides executable commands and expected success behavior.
6. Every `Related docs and modules` section contains at least two relevant links/references.
7. `Docs/Generators/AutoPacker.md` is refactored into the shared template, not left in legacy structure.
8. `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` in this ExecPlan are updated to reflect final implementation reality.

## Idempotence and Recovery

This plan is safe to re-run. Documentation writes are deterministic and can be repeated without side effects beyond file content replacement.

If a module draft is interrupted, resume by re-running that module's Planning phase and then Drafting phase before moving to the next milestone. Do not skip phases.

If a validation step reports missing headings or invalid links, fix only the affected doc and re-run validation until clean.

## Artifacts and Notes

Evidence to preserve during execution:

- Short command outputs proving each target doc exists.
- Validation command outputs proving required sections are present.
- Brief notes in `Surprises & Discoveries` when module behavior differs from assumptions.

## Interfaces and Dependencies

No runtime code interfaces are changed by this initiative. Documentation must reflect existing interfaces only.

Module dependencies and public API references must be derived from existing sources in each module's `Runtime/`, `Container/`, `Samples/`, and `Tests/` directories, plus generator contracts for AutoPacker in `Generators/AutoPacker/src/Contracts/`.

---

Revision Note (2026-03-09): Initial version created to execute repository-wide module documentation coverage for the nine architecture modules using a shared template and milestone-based planning+drafting flow.
Revision Note (2026-03-09): Completed Milestone 1 by creating `Docs/Core/MVVM.md` from inspected runtime contracts, samples, and tests, and updated living sections to reflect milestone evidence.
Revision Note (2026-03-09): Refined template contract after MVVM feedback to enforce API-first usage and dedicated sections for architecture/internal behaviors.
Revision Note (2026-03-09): Completed Milestone 2 by creating `Docs/Infra/Containers.md` using the upgraded template contract and module evidence from contracts, sample, and tests.
Revision Note (2026-03-09): Completed Milestone 3 by creating `Docs/Infra/Events.md` using module contracts, implementation, installer wiring, samples, and tests.
Revision Note (2026-03-09): Completed Milestone 4 by creating `Docs/Infra/Navigation.md` from navigation contracts, orchestration internals, installer wiring, samples, and tests.
Revision Note (2026-03-09): Expanded `Docs/Infra/Navigation.md` with explicit stack behavior (open/close/return), operation timing, view lifecycle callback guidance, `ViewConfig` explanation, and transition handler setup details.
Revision Note (2026-03-09): Completed Milestone 5 by creating `Docs/Infra/NetworkMessages.md` from contract, dispatcher implementation, serialization model, samples, and tests.
Revision Note (2026-03-09): Completed Milestone 6 by refactoring `Docs/Generators/AutoPacker.md` into the upgraded template with generator internals and contract-based usage/test flow.
Revision Note (2026-03-09): Completed Milestone 7 by creating `Docs/Tools/Maps.md` with index/indexer behavior, architecture snippets, and test guidance.
Revision Note (2026-03-09): Completed Milestone 8 by creating `Docs/Tools/Records.md` with shim purpose, usage constraints, and test guidance.
Revision Note (2026-03-09): Completed Milestone 9 by creating `Docs/Tools/Types.md` with runtime/editor architecture, usage patterns, and dependency extraction details.
Revision Note (2026-03-09): Completed Milestone 10 consolidation by validating required headings and section consistency across all 9 target docs.
