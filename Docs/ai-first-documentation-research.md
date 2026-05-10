---
title: AI-First Pre-Implementation Documentation — Research Compilation
status: research-handoff
created: 2026-05-10
purpose: >
  Compiled research on industry standards, platforms, and best practices for
  AI-first technical documentation in the pre-implementation (planning, specs,
  architectural definition) stage. Intended as a self-contained handoff for a
  sibling project pursuing the same goal.
audience: secondary project team building an AI-first documentation system
scope:
  - landscape of standards and frameworks (2024-2026)
  - recurring structural patterns
  - cross-slice / cross-artifact dependency management
  - AI-first vs human-first authoring debate
  - docs-first platforms (Backstage, ContextMapper, C4, DDD-Crew, etc.)
  - heterogeneous-artifact modelling (system vs domain object)
  - copy-pasteable templates
---

# AI-First Pre-Implementation Documentation: State of the Art

This document compiles two rounds of deep research on AI-first pre-implementation documentation:

1. **Part I** — Industry standards, recurring patterns, and the AI-first vs human-first debate.
2. **Part II** — Docs-first platforms with typed artifacts, and how they handle heterogeneous artifacts (system vs domain object) that depend on each other.

It is intended as a self-contained briefing for a sibling project. All claims cite real sources; URLs are inline.

---

# Part I — The AI-First Documentation Landscape

## 1. TL;DR

- **"Spec-driven development" has consolidated** in 2025-2026 around two flagship implementations: GitHub's **Spec Kit** (open-source, agent-agnostic, four-phase workflow: specify → plan → tasks → implement) and AWS's **Kiro** (three-file requirements/design/tasks structure). Both treat the spec as the primary artifact and code as its expression.
- **EARS** (Easy Approach to Requirements Syntax — `WHEN <trigger> THE SYSTEM SHALL <response>`) is winning as the canonical machine-parseable acceptance-criteria notation. Kiro adopts it natively; Spec Kit has an open issue to integrate it.
- **Memory files have standardized around `AGENTS.md` as the cross-tool universal**, with `CLAUDE.md` and `.cursor/rules/*.mdc` layered on top. Strong consensus: keep them ≤300 lines, reference rather than inline.
- **`llms.txt`** (Jeremy Howard, Sept 2024) has real adoption — Anthropic, Stripe, Cursor, Vercel, Supabase publish them. Format: `H1` + `> blockquote summary` + `H2` sections of annotated links. Being repurposed as a project-level index, not just a website file.
- **Vertical slices are the default unit of AI work.** Cross-slice dependencies are mostly solved by (a) explicit `[NEEDS CLARIFICATION]` markers, (b) parallelism markers `[P]`, (c) a shared **constitution.md** rather than N×N backlinks.
- **Progressive disclosure is the dominant context-engineering pattern** — Anthropic Skills load only YAML descriptions at startup, full body when triggered, references on demand. SKILL.md ≤500 lines.
- **AI-first vs human-first is converging on "structurally AI-first, rendered human-first"** — single Markdown source with rich front-matter, transformed for humans via Mintlify/Docusaurus. Mintlify reports ~45% of doc traffic now comes from AI agents. **Maintaining two artifacts (human PRD + AI prompt pack) is the widely-cited failure mode.**

## 2. Landscape — three layers

### Layer 1 — Foundational standards still load-bearing

- **ADRs** (Nygard, 2011): Title / Status / Context / Decision / Consequences. Joel Parker Henderson's repo catalogs ~20 variants (MADR, Y-Statements, etc.). Martin Fowler endorses the lightweight format.
- **EARS** (Mavin et al., 2009): five sentence templates — Ubiquitous, State-driven (`While`), Event-driven (`When`), Optional (`Where`), Complex. Used by Airbus, NASA, Rolls-Royce; now picked up by AI tools because it parses cleanly.
- **arc42 + C4** still the de facto architecture skeleton; 2026 saw an academic extension **RAD-AI** adding 8 AI-specific arc42 sections.
- **Squarespace's "Yes, if" RFC** is the most-cited public RFC template; Stripe's adds RFC2119 keyword discipline (MUST/SHALL/SHOULD).
- **Amazon PR/FAQ + 6-pager** ("Working Backwards"): narrative form, press release first, FAQ next, read silently at the start of the meeting.
- **Shape Up pitch** (Basecamp): Problem / Appetite / Solution / Rabbit-holes / **No-gos**. The "no-gos" section anticipates one of the AI-spec needs (out-of-scope hardening).
- **Diátaxis** (Procida): tutorials / how-to / reference / explanation. Adopted by Canonical/Ubuntu, LangChain, StreamingFast. Relevance for AI is real but indirect — clean separation improves retrieval and skill targeting.
- **Eugene Yan's ML Design Doc Template** — widely cloned: Overview / Motivation / Success Metrics / Requirements & Constraints (in/out of scope) / Methodology / Implementation / Appendix.

### Layer 2 — AI-native frameworks (2024-2026)

- **GitHub Spec Kit** — `/speckit.specify` → `/speckit.plan` → `/speckit.tasks` → `/speckit.implement`. Templates use `[NEEDS CLARIFICATION]` markers, P1/P2/P3 priorities, `[P]` parallel markers, and a project-level **constitution.md** that encodes immutable principles (TDD, library-first, anti-abstraction).
- **AWS Kiro** — three files per spec: `requirements.md` (user stories + EARS acceptance criteria), `design.md` (technical architecture), `tasks.md` (checklist). Plus "steering rules" (project-wide) and "hooks" (file-event triggers).
- **Anthropic Agent Skills** — `SKILL.md` with YAML front-matter (`name`, `description` are the only required fields; `description` is the trigger and should be "pushy"). Body ≤500 lines; deeper content in `references/`, executable code in `scripts/`, output assets in `assets/`. Three-level **progressive disclosure**: metadata always loaded, body on trigger, refs on demand.
- **Cursor Rules** — modern format is `.cursor/rules/*.mdc` (replacing legacy single `.cursorrules`). Each `.mdc` rule has front-matter declaring scope (always / auto-attached via globs / agent-requested / manual).
- **CLAUDE.md / AGENTS.md** — `AGENTS.md` is emerging as the universal cross-tool spec (read by Claude Code, Cursor, Copilot, Aider, Zed, Warp, Gemini CLI, Windsurf, RooCode). `CLAUDE.md` layers Claude-specific instructions on top.
- **`llms.txt` / `llms-full.txt`** — H1 site name + `> blockquote summary` + H2 sections of annotated relative links. `llms-full.txt` inlines the full content; AI agents fetch it at >2× the rate of `llms.txt`.
- **ChatPRD / Miqdad Jaffer's AI PRD** — opinionated PRD templates aimed at AI agents.

### Layer 3 — Tooling that consumes the docs

Mintlify, Backstage TechDocs, Docusaurus (with llms.txt plugin), MkDocs, Astro Starlight, **Fern** (AI doc linter + drift detection), **repowise-dev** (post-commit "wiki is stale" via MCP), Swimm (code-doc relationships).

## 3. Recurring structural patterns

Distilled across Spec Kit, Kiro, Anthropic Skills, Squarespace RFC, ML Design Doc, Shape Up pitch, and the Mintlify/Cursor convention. Every good AI-first pre-implementation spec contains:

1. **YAML front-matter** with at minimum `id`, `status` (Draft/Proposed/Accepted/Implemented/Deprecated), `created`, `description`. Optional: `owner`, `version`, `priority`, `dependencies`, `related`, `slices`. Anthropic emphasizes `description` is the *primary triggering mechanism* and should be deliberately verbose about *when* to apply the doc.
2. **One-paragraph summary** directly under the title — this is what gets retrieved/embedded.
3. **Context / Background / Motivation** (universal — ADR, RFC, Shape Up, PR/FAQ, Spec Kit, Kiro).
4. **Goals AND Non-goals / Out-of-scope / No-gos.** Shape Up's "no-gos" is the strongest version. AI agents will gold-plate without this.
5. **User stories or scenarios in parseable form.** Spec Kit uses Given/When/Then under prioritized P1/P2/P3 stories. Kiro uses EARS. Both testable and machine-parseable.
6. **Functional requirements with stable IDs** (`FR-001`, `FR-002`; Stripe RFC: RFC2119 MUST/SHALL keywords; Kiro: numbered acceptance criteria). Stable IDs enable cross-doc references that survive edits.
7. **Interface contracts before prose** — Spec Kit's plan phase generates `data-model.md`, `contracts/`, `quickstart.md` *before* tasks. Function signatures, JSON schemas, types appear before prose.
8. **`[NEEDS CLARIFICATION: ...]` markers** — Spec Kit's distinctive contribution. Forces the agent to *not assume*.
9. **Measurable success criteria** (`SC-001`, `SC-002` …) — technology-agnostic and measurable.
10. **Definition of Done / Verification** — checklist that doubles as the test plan.
11. **Risks / Alternatives / Complexity tracking** — Squarespace RFC has explicit "Risks" + "Alternatives Considered"; Spec Kit's plan template has a "Complexity Tracking" table forcing justification when violating the constitution.
12. **Cross-references** — plain markdown links + `dependencies:` arrays in front-matter. **No production AI workflow uses Obsidian-style `[[wiki-links]]`** — they don't survive renames and agents don't follow them automatically.
13. **Trigger context for the AI** — short, "pushy" `description` field telling the agent *when* to consult this doc (Anthropic Skills convention).

## 4. Cross-slice dependency management

This is the area where practice is least mature. What works in 2026:

- **Constitution / Steering files** — Spec Kit's `constitution.md` and Kiro's "steering rules" hold project-wide invariants so each slice doesn't re-derive them. **Single most effective pattern** — instead of N×N backlinks, every slice points to one shared file.
- **Front-matter `dependencies:` arrays + `related:` links** — declarative, lintable.
- **Stable IDs** (`FR-001`, `SC-001`, `T012`) so a downstream slice can reference "FR-014 of slice 042-billing" without breaking when text changes.
- **`[P]` parallel markers + dependency-ordered task lists** — explicit "this task depends on T004 and is parallelizable with T009".
- **Doc lint/CI**: Fern (AI-driven, auto-fixes findings via PRs), Vale (rule-based prose linter), markdown-link-check, `lychee`.
- **Drift detection**: Swimm and repowise-dev do AST-aware links between code symbols and docs, flagging docs when referenced code changes. Anthropic-style alternative: a "doc-keeper" sub-agent runs nightly with the diff and flags stale-looking specs.
- **Backstage TechDocs** for org-scale: each slice/component is a Backstage entity with metadata.yaml; relations are first-class and queryable.
- **What did NOT work**: Obsidian-style bidirectional `[[wiki-links]]` — popular for personal knowledge work but agents don't follow them and they don't survive renames cleanly. **Plain relative paths + front-matter dependency arrays is the pragmatic winner.**

## 5. AI-first vs human-first — where 2026 has landed

**Converging on "structurally AI-first, presentation human-first," single source.**

Evidence:
- **Mintlify's thesis** is that docs are an interface for *both* audiences (~45% AI traffic). Their solution: structured Markdown → auto-generate `llms.txt`, `llms-full.txt`, MCP server.
- **Anthropic's Skills** are unapologetically AI-first (the YAML description is *for the model*) but still readable Markdown.
- **Spec Kit and Kiro** produce Markdown humans review; phase gates *add* human review checkpoints. In practice these specs are often longer than human-only PRDs, not shorter.
- **The two-artifacts approach is losing** — maintaining a human PRD and an "AI prompt pack" separately is the widely-cited failure mode (Addy Osmani, Pragmatic Engineer, ChatPRD).
- **Contrarian take** (Simon Willison): aggressive AI-first formatting can degrade human scannability when YAML and `[NEEDS CLARIFICATION]` blocks dominate. Mitigation is **renderer-side, not authoring-side** — Mintlify or custom Markdown transformers can hide/style front-matter for humans.

Bottom line in mid-2026: **one Markdown source per artifact, with rich front-matter, opinionated section structure, and machine-parseable acceptance criteria, then transform for humans.**

## 6. Recommended template skeletons (canonical)

Four copy-pasteable templates, ordered broadest to narrowest. All four can coexist in one repo.

### Template A — Spec Kit "Feature Specification" (the AI-first PRD)

Source: [github/spec-kit/templates/spec-template.md](https://github.com/github/spec-kit/blob/main/templates/spec-template.md). Best for a vertical-slice spec consumed by Claude Code / Cursor / Copilot.

```markdown
# Feature Specification: [FEATURE NAME]

**Feature Branch**: `[###-feature-name]`
**Created**: [DATE]
**Status**: Draft
**Input**: User description: "$ARGUMENTS"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - [Brief Title] (Priority: P1)
[user journey in plain language]
**Why this priority**: [value + reason for P1]
**Independent Test**: [how to verify in isolation - must be true MVP-on-its-own]
**Acceptance Scenarios**:
1. **Given** [state], **When** [action], **Then** [outcome]

### User Story 2 ... (Priority: P2)
### User Story 3 ... (Priority: P3)

### Edge Cases
- What happens when [boundary]?
- How does the system handle [error scenario]?

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001**: System MUST [capability]
- **FR-002**: Users MUST be able to [interaction]
- **FR-006**: System MUST authenticate via [NEEDS CLARIFICATION: SSO? OAuth?]

### Key Entities *(if data involved)*
- **[Entity]**: [meaning + key attributes - no implementation]

## Success Criteria *(mandatory)*
- **SC-001**: [Measurable outcome - tech-agnostic]
- **SC-002**: [Performance / scale metric]

## Assumptions
- [Scope boundary, dependency, environment assumption]
```

Why it works for AI: P1/P2/P3 priorities map to "ship MVP first"; `[NEEDS CLARIFICATION]` prevents silent assumptions; `FR-NNN` IDs survive edits; Given/When/Then is testable.

### Template B — Kiro three-file slice (requirements / design / tasks)

Source: [kiro.dev/docs/specs](https://kiro.dev/docs/specs/). Best when you want EARS-formatted requirements and clean separation of "what" / "how" / "do".

```markdown
<!-- requirements.md -->
---
id: 042-workspace-invites
status: accepted
owner: @alice
related: [041-auth, 050-billing]
---
# Workspace Invites — Requirements

## User Story
As a workspace admin, I want to invite users by email,
so that they can join my workspace without me sharing credentials.

## Acceptance Criteria (EARS)
- **R1**  WHEN an admin submits an invite form with a valid email
         THE SYSTEM SHALL create an invite record and send a signed-link email.
- **R2**  IF the email already belongs to a workspace member
         THEN THE SYSTEM SHALL return "User already in workspace" without creating an invite.
- **R3**  WHILE an invite is pending
         THE SYSTEM SHALL allow the admin to revoke it.
- **R4**  WHERE SSO is enabled for the workspace
         THE SYSTEM SHALL require the invitee to authenticate via SSO before joining.
```

```markdown
<!-- design.md -->
# Workspace Invites — Design
## Architecture Overview
[diagram or prose]
## Data Model
- `Invite { id, workspace_id, email, status, signed_token, expires_at }`
## API Contracts
- `POST /workspaces/:id/invites` → 201 {invite_id} | 409 {error: "already_member"}
- `POST /invites/:token/accept`  → 200 {workspace_id}
## Sequence
1. Admin POST → service issues signed JWT (24h) → email sent
2. Invitee clicks link → /invites/:token/accept → membership row written
## Open Questions / [NEEDS CLARIFICATION]
- Token rotation policy on revoke?
```

```markdown
<!-- tasks.md -->
# Workspace Invites — Tasks
- [ ] T001 [P] Create `Invite` migration (`db/migrations/...`)
- [ ] T002 [P] Add `POST /workspaces/:id/invites` handler (`api/invites.ts`)
- [ ] T003     Wire signed-token generator (depends on T001)
- [ ] T004 [P] Email template (`emails/invite.tsx`)
- [ ] T005     Acceptance test for R1 (`tests/invites.spec.ts`)
- [ ] T006     Acceptance test for R2 (depends on T002)
```

Why it works for AI: EARS removes ambiguity; the three-file split lets the agent load only what it needs at each phase; `[P]` markers + explicit `depends on` enable parallel sub-agents.

### Template C — Anthropic Skill / per-slice CLAUDE.md companion

Source: [skills/skill-creator/SKILL.md](https://github.com/anthropics/skills/blob/main/skills/skill-creator/SKILL.md). Best for a per-slice "how to work in this slice" file that the agent loads on demand.

```markdown
---
name: workspace-invites-slice
description: Use this skill whenever editing files under apps/web/invites/ or
  api/src/invites/, or when the user mentions "workspace invite", "invite link",
  or "invite expiry". It explains the data model, contracts, and conventions for
  the workspace-invites slice. Make sure to consult this skill BEFORE writing any
  code that touches the Invite entity.
---

# Workspace Invites Slice

## What this slice owns
- `apps/web/invites/**`, `api/src/invites/**`, migrations `2026_05_*_invites.sql`

## Conventions specific to this slice
- All invite tokens use the `inv_` prefix and are signed with `INVITE_SIGNING_KEY`.
- Never expose raw tokens in logs - use `redact_token()` from `lib/redact.ts`.

## Canonical examples
- Happy-path test: `tests/invites/accept.spec.ts`
- Migration template: `db/migrations/templates/invite-like.sql`

## Cross-slice contracts
- Reads `Workspace` from slice 040-workspaces (read-only)
- Emits `invite.accepted` event consumed by slice 050-billing

## When NOT to use this skill
- Generic auth questions → see `auth-slice` skill
- Email rendering → see `transactional-email` skill
```

Why it works for AI: front-matter `description` is the trigger; body short (≤500 lines target); references concrete canonical examples instead of duplicating code; explicit "when NOT to use" reduces over-triggering.

### Template D — Project-level llms.txt (the index)

Source: [llmstxt.org](https://llmstxt.org/). Best for the "table of contents an agent loads first."

```markdown
# MyProject

> MyProject is a multi-tenant workspace SaaS written in TypeScript on Next.js 15
> and Postgres. Specs live under /specs/, code under /apps/ and /api/.

## Constitution / project-wide rules
- [Constitution](/CONSTITUTION.md): immutable principles (TDD, library-first, no shadow APIs)
- [Agent guide](/AGENTS.md): cross-tool agent instructions
- [Claude-specific notes](/CLAUDE.md): only Claude Code overrides

## Active feature specs
- [042 Workspace Invites](/specs/042-workspace-invites/spec.md): admin email invites with signed links
- [043 SAML SSO](/specs/043-saml-sso/spec.md): enterprise SSO via SAML 2.0
- [050 Billing v2](/specs/050-billing-v2/spec.md): seat-based metering

## Architecture references
- [System overview](/docs/architecture/overview.md): C4 context + container diagrams
- [Data model](/docs/architecture/data-model.md): canonical entities

## Optional
- [ADR index](/docs/adrs/README.md): historical decisions
- [Glossary](/docs/glossary.md)
```

The `## Optional` section is a llms.txt convention meaning "skip if context is tight."

(For ADRs, use Nygard's classic Title / Status / Context / Decision / Consequences — the gold standard, fine inside an AI-first repo.)

## 7. Open questions / tradeoffs

1. **EARS vs Given/When/Then** — pick one. EARS more compact and parses better; G/W/T more familiar to QA. Mixing within one project causes friction.
2. **Single-file specs vs Kiro three-file split** — three-file gives clean phase gates and lets the agent load less context per phase, but triples file count. For small teams a single-file spec (Template A) plus per-slice skill (Template C) often works better.
3. **Constitution vs duplicating rules per spec** — strongly favor constitution. Duplication failure mode shows up at ~5+ slices.
4. **"Pushy" skill descriptions** — Anthropic guidance is to over-trigger rather than under-trigger; verbose for humans but renderer-side hiding is fine.
5. **`[NEEDS CLARIFICATION]` placement** — inline (Spec Kit) good for agents, ugly for humans. Some teams move them to a dedicated `## Open Questions` section. Tradeoff: harder for the agent to surface them in context.
6. **Cross-slice updates** — two viable answers: (a) front-matter `dependencies:` + CI doc-lint that fails when a referenced ID disappears (cheaper, deterministic); (b) nightly "doc-keeper" sub-agent that diff-checks specs against code (catches semantic drift).
7. **`llms.txt` at project root vs only website root** — spec is ambiguous; in practice teams use both. Project-root is more useful for coding agents with filesystem access.
8. **Token-budget discipline** — descriptions ≤300 chars, CLAUDE.md ≤300 lines, skill bodies ≤500 lines, deeper content in `references/`. Worth enforcing as lint rules.

---

# Part II — Docs-First Frameworks and Heterogeneous Artifacts

## 8. Direct answer

If you want a single, file-based, *typed-artifact* framework whose explicit purpose is to plan a system before any code is written, **ContextMapper (CML DSL)** is the strongest pure-play candidate, with **Backstage's Software Catalog** a close second for anything broader than a single bounded context.

ContextMapper natively distinguishes `BoundedContext`, `Aggregate`, `Entity`, `ValueObject`, `Service`, `Module`, `Domain`/`Subdomain`, and gives you typed *relationships* between contexts (Customer/Supplier, Conformist, ACL, Open Host Service, Published Language, Shared Kernel, Partnership) — exactly the "system vs domain object that lives in it" problem. Backstage's catalog (`System`, `Component`, `API`, `Resource`, `Domain`) is the strongest *industrial* answer with a typed graph (`partOf`, `dependsOn`, `providesApi`, `consumesApi`, `ownedBy`), but it stops above the level of an individual domain object.

**No off-the-shelf product does the whole job.** The closest single platform is Backstage + TechDocs, and it's not really planning-first. Real practice composes: **C4/Structurizr for architecture, ContextMapper or DDD-Crew canvases for domain, arc42 as prose container, ADRs for decisions.**

## 9. Platform-by-platform survey

### Strong candidates

**ContextMapper (CML DSL)** — Textual DSL (`.cml` files) for DDD strategic + tactical modelling. First-class types: `BoundedContext`, `Aggregate` (with `aggregateRoot`), `Entity`, `ValueObject`, `Domain`, `Subdomain`, `Module`, `Service`, `DomainEvent`, `Repository`. Inter-context relationships are typed: `Upstream-Downstream` with roles `OHS`/`PL`/`CF`/`ACL`, plus `Customer-Supplier`, `Partnership`, `Shared Kernel`. Ships *Architectural Refactorings* (AR-1 Split Aggregate, AR-6 Merge Aggregates, AR-7 Merge Bounded Contexts, AR-8 Extract Shared Kernel, AR-10 Change Shared Kernel to Partnership, etc.) — the model itself supports evolution operations. AI-friendly: plain text, Eclipse-based tooling but files perfectly editable by an LLM. **Verdict: best fit when your project decomposes into domains and aggregates.** [contextmapper.org](https://contextmapper.org/docs/bounded-context/)

**Backstage Software Catalog** — YAML descriptors (`catalog-info.yaml`) declaring entities of kind `Component`, `System`, `API`, `Resource`, `Domain`, `Group`, `User`, `Location`, with relations `partOf`/`hasPart`, `dependsOn`/`dependencyOf`, `providesApi`/`apiProvidedBy`, `consumesApi`/`apiConsumedBy`, `ownedBy`/`ownerOf`, `parentOf`/`childOf`, `memberOf`/`hasMember`. Highly AI-friendly. The kind set is extensible (custom kinds). **No first-class "domain object" entity** — Backstage stops at the deployable-unit level. **Verdict: best for organization-wide system catalogs**, not for documenting an individual entity's invariants. [backstage.io](https://backstage.io/docs/features/software-catalog/system-model/)

**C4 model + Structurizr DSL** — Text DSL (`workspace.dsl`) with four typed levels: `person`, `softwareSystem`, `container`, `component`, plus deployment elements. Relationships are first-class with implied propagation up the hierarchy. Stereotypes via `tags`. C4 is deliberately architecture-only — you would not put `Card.health` in a C4 diagram. **Verdict: the architecture spine** of any docs-first stack; pair with something domain-aware. [docs.structurizr.com](https://docs.structurizr.com/dsl/language)

**arc42** — 12-section opinionated architecture template (Goals, Constraints, Context, Solution Strategy, Building Block View, Runtime View, Deployment, Crosscutting, Decisions, Quality Requirements, Risks, Glossary). Available in [Markdown](https://github.com/NetworkedAssets/arc42-in-markdown-template). Section 5 is *recursively* hierarchical — each block expands into its own building block view with the same structure. arc42 is prose, not typed artifacts — **the container, not the type system.**

**DDD-Crew canvases** — Concrete, distinct templates per artifact type: [Bounded Context Canvas](https://github.com/ddd-crew/bounded-context-canvas), [Aggregate Design Canvas](https://github.com/ddd-crew/aggregate-design-canvas), Domain Message Flow Modelling, CoMo Prep Canvas. Originally PDF/Miro but with a [community Markdown port of BCC](https://github.com/grjsmith/bounded_context_canvas_md). The Aggregate Design Canvas was *explicitly* inspired by the BCC, so the typing distinction (system-ish vs object-ish) is a deliberate design choice. **Verdict: best fit for direct, lightweight typed templates without a tool install.**

**GitHub Spec Kit** — Already covered; flat per-feature Markdown, no typed graph between artifacts. Recently moved to a [pluggable preset/template system](https://github.com/github/spec-kit) so you can register custom artifact types. **Verdict: workflow shell, not typed-artifact framework.**

**AWS Kiro** — Three artifact types per spec, plus hooks. Like Spec Kit, flat per-feature; no graph of typed entities. AI-native (the IDE is the agent). **Verdict: same as Spec Kit but more opinionated and IDE-locked.**

**Domain Storytelling / EventStorming + Egon.io** — Methodologies for the discovery phase. [Egon.io](https://egon.io) produces SVG/JSON of domain stories. Excellent *upstream* of ContextMapper or DDD-Crew canvases. **Verdict: discovery, not specification.**

**OpenAPI / AsyncAPI / Stoplight / Specmatic** — Contract-first for APIs only. Models the wire interface, not the domain object itself. **Verdict: complementary, not a docs-first framework.**

### Doesn't fit (one-line dismissals)

- **TLA+ / Alloy** — formal specification of behaviors/invariants; valuable but narrow and steep.
- **Aha! / Productboard / Linear / Notion PRDs** — product-planning-first; UI-locked, AI-hostile.
- **Confluence ADR + Tech-Spec + PRD blueprints** — wiki templates, not a typed graph; AI-hostile.
- **Mintlify, Fern, Docusaurus, GitBook** — post-implementation documentation sites.
- **Backstage TechDocs** (vs the Catalog) — Markdown publishing layer hanging off a Catalog entity.
- **Obsidian + Excalidraw + Dataview** — viable solo, AI-friendly, but typing is purely convention you invent. Mention as the lightweight alternative.
- **GDD / Riot tech design docs** — flat prose; useful as section-list reference for *gameplay* design layer.

## 10. The heterogeneous-artifacts question

### 10.1 Which frameworks have *typed* artifact templates?

| Framework | Typed | "System" type | "Domain object" type |
|---|---|---|---|
| ContextMapper | Yes (DSL) | `BoundedContext`, `Subdomain` | `Aggregate`, `Entity`, `ValueObject` |
| Backstage | Yes (YAML kinds) | `System`, `Component` | none below `Component` (extensible) |
| C4/Structurizr | Yes (4 levels) | `softwareSystem`, `container` | `component` (still architectural) |
| DDD-Crew | Yes (canvases) | Bounded Context Canvas | Aggregate Design Canvas |
| arc42 | No (prose) | section 5 building block | not first-class |
| Spec Kit / Kiro | No (flat) | n/a | n/a |

**ContextMapper and DDD-Crew are the only options where a "system" template and a "domain object" template are first-class peers with a defined relationship.** Backstage is rich on systems but flat below; C4 is rich on systems but doesn't go domain-deep.

### 10.2 How is "system contains domain object" modelled?

- **ContextMapper:** the `BoundedContext` block literally contains `Aggregate` blocks, which contain `Entity` blocks. Containment *is* the language. Cross-context relationships are typed (`Upstream-Downstream[OHS, PL] : CardSystem -> Inventory`).
- **Backstage:** `partOf`/`hasPart` between `Component` and `System`; `dependsOn` between `Component` and `Resource`. To model a domain object, invent a custom kind (`kind: DomainObject`) and a custom relation, or stuff into TechDocs Markdown.
- **C4/Structurizr:** containment via lexical nesting in the DSL; domain objects live one level *below* C4's lowest tier and usually only mentioned in component descriptions or tags.
- **DDD-Crew:** no formal link — the BCC names its aggregates in the "Ubiquitous Language" / "Inbound/Outbound Messages" sections; the ADC names its parent context as a header field. Linkage is editorial.
- **arc42:** by recursive section-5 nesting; domain objects show up in §8 (Crosscutting Concepts) or the Glossary.

### 10.3 "If you change X, revisit Y" mechanisms

- **ContextMapper** has architectural refactorings (AR-1 to AR-11) that perform cross-cutting edits in one operation — splitting an aggregate, merging contexts, extracting a shared kernel — and a validator that flags inconsistencies in the model.
- **Backstage** has a relations graph queryable via the catalog API + the `relations` tab in the UI; you can run lint plugins and add custom processors that fail CI when relations break. No built-in "diff awareness," but the graph is explicit, so trivial to script.
- **Structurizr** has a "deviations" concept in the validating workspace tooling, but no automatic propagation — re-render and read.
- **Spec Kit, Kiro, arc42, DDD-Crew canvases** — none have automated propagation. **You either rely on agent re-reads or wire your own checks.**

For an AI-first workflow this is where you lean into LLMs: any of these formats parse cleanly, and a Claude skill / `CLAUDE.md` instruction like "when editing a `BoundedContext`, re-read every `Aggregate` it contains and flag invariants that may have shifted" gives you the propagation mechanism the tools lack.

### 10.4 Concrete example: Card System / Card mapped across frameworks

| Framework | Card System is… | Card is… | The "contains" link |
|---|---|---|---|
| **ContextMapper** | `BoundedContext CardSystem` (or `Subdomain` + `BoundedContext`) | `Aggregate Card { Entity Card aggregateRoot; ValueObject Cost; }` | lexical nesting inside the `BoundedContext`'s `Aggregate { … }` |
| **Backstage** | `kind: System, name: card-system` | `kind: Component, name: card` (best fit) **or** custom `kind: DomainObject` | `spec.system: card-system` on the Card entity ⇒ `partOf` relation |
| **C4 / Structurizr** | `container "Card System" { … }` inside the game's `softwareSystem` | a `component "Card"` *or*, more honestly, a tag on the data layer — C4 doesn't really do it | lexical nesting + an `implied relationship` from any user of the component |
| **DDD-Crew** | one Bounded Context Canvas titled "Card System" | one Aggregate Design Canvas titled "Card", listing Card System as the parent context in the header | editorial — naming convention plus a parent-context field |

The cleanest mapping is **ContextMapper** because the language was designed for exactly this distinction. The cleanest *operational* mapping (tied into a real running catalog) is **Backstage** if you accept "Component" as a slight abuse for a domain object — or define a `DomainObject` custom kind and a `containsObject` relation via [extending the model](https://backstage.io/docs/features/software-catalog/extending-the-model/).

## 11. Recommended stack

For a solo / small-team setup that's Markdown-native, AI-first, and avoids heavy tooling:

1. **DDD-Crew canvases in Markdown** as the typed-artifact spine. One Bounded Context Canvas per subsystem and one Aggregate Design Canvas per first-class domain object. Use the [grjsmith Markdown port](https://github.com/grjsmith/bounded_context_canvas_md) for BCC and copy-adapt the [official ADC](https://github.com/ddd-crew/aggregate-design-canvas) into Markdown.
2. **Structurizr DSL** as the architecture spine. One `workspace.dsl` defining `softwareSystem` with containers per subsystem. This is your C4 picture and stays in sync with the canvases by naming convention.
3. **arc42 (Markdown port)** as the prose container — only when you need long-form. Don't fill it in early; it's an outline you grow into.
4. **ADRs** (one Markdown file per decision, [adr-tools](https://github.com/npryce/adr-tools) format) for "why."
5. **CLAUDE.md / AGENTS.md instructions** that codify the typing: "When the user asks to change `<Aggregate>`, re-read `<context>.bcc.md` and any ADR mentioning that aggregate; flag any invariant section that may now be wrong." This is the propagation mechanism the tooling lacks.

Optional secondary:
- **GitHub Spec Kit** as the *workflow* shell on top of the above, with custom presets that register `bounded-context.md` and `aggregate.md` as artifact types.
- **ContextMapper** if the project grows multi-context enough to justify the DSL — the refactorings then earn their keep.
- **Backstage** only if cataloguing many systems across an org. Overkill for solo work.

Avoid: Kiro (IDE-locked, AI-hostile to non-Kiro agents), Confluence/Notion (UI-locked), Mintlify/GitBook (post-hoc).

## 12. Concrete typed-artifact templates (system + object)

Two short Markdown templates that demonstrate the typing distinction. **Deliberately distinct shapes** — the system template is collaboration-shaped, the object template is invariant/lifecycle-shaped.

### `docs/contexts/<context>.bcc.md` — Bounded Context Canvas (system-shaped)

```markdown
---
kind: BoundedContext
name: CardSystem
domain: Gameplay
classification: core           # core | supporting | generic
status: design
owners: [solo]
relatedContexts:
  - { name: BattleSystem, role: downstream, pattern: ACL }
  - { name: Deck,         role: upstream,   pattern: OHS }
aggregates: [Card, CardEffect]
---

# Card System

## Purpose
One paragraph: what business capability this context owns.

## Strategic classification
Core / Supporting / Generic + reason.

## Ubiquitous language
- Card, Cost, Effect, Trigger, Zone, Reveal …

## Inbound communication (what this context receives)
| Sender | Message | Type (cmd/evt/qry) | Description |

## Outbound communication (what this context emits)
| Receiver | Message | Type | Description |

## Dependencies & policies
- Depends on Deck (OHS), exposes a Published Language for BattleSystem.

## Open questions / risks
- …

## Linked aggregates
- [Card](../aggregates/card.adc.md)
- [CardEffect](../aggregates/card-effect.adc.md)
```

### `docs/aggregates/<aggregate>.adc.md` — Aggregate Design Canvas (object-shaped)

```markdown
---
kind: Aggregate
name: Card
boundedContext: CardSystem        # back-reference, must match a BCC file
aggregateRoot: Card
entities: [Card]
valueObjects: [Cost, Rarity, EffectRef]
status: design
---

# Card

## Description
One paragraph: what a Card *is* in the ubiquitous language.

## Aggregate root & invariants
- A Card has exactly one Cost.
- A Card's Rarity is immutable after creation.
- A Card may reference 0..N Effects, each by EffectRef.

## State model (lifecycle)
Created → InDeck → InHand → OnBoard → Discarded → Exiled
(Allowed transitions; forbidden transitions explicitly listed.)

## Commands handled
- CreateCard(spec)
- PlayCard(target)
- DiscardCard(reason)

## Domain events emitted
- CardCreated, CardPlayed, CardDiscarded, CardExiled

## Throughput / consistency
- Strong consistency within aggregate; eventual across BattleSystem.

## Schema (host-language reference)
Pointer to the implementation type, e.g. `Assets/.../CardData.cs` (Unity) or
`packages/domain/card.ts` (TS).

## Examples
- Goblin (cost 1, rarity common, effects [Strike(1)])
- Dragon (cost 7, rarity rare,   effects [Strike(5), Burn(2)])

## Open questions
- …
```

The two templates *look different on purpose* — the BCC is collaboration-shaped, the ADC is invariant/lifecycle-shaped — and they cross-link through the YAML frontmatter (`boundedContext` on the aggregate, `aggregates` on the context). **That YAML is what lets a Claude skill enforce: "if you edit `card.adc.md`, also re-read `card-system.bcc.md` and any ADR that mentions either,"** giving you the propagation mechanism the tooling itself doesn't ship.

---

# Part III — Synthesis for the Receiving Project

If you are starting an AI-first documentation system from scratch, the research collapses to these decisions:

1. **One Markdown source per artifact, with YAML front-matter, in a Git repository.** Not a wiki, not Notion, not Confluence. AI agents need filesystem access and stable paths.

2. **Pick a typed-artifact vocabulary.** Without typing, every spec converges to the same shape and you lose the system-vs-object distinction. Cheapest path: DDD-Crew canvases (BCC + ADC). Strongest path: ContextMapper CML. Industrial path: Backstage Software Catalog with custom kinds.

3. **Use stable IDs (`FR-001`, `SC-001`, `AGG-CARD`, `CTX-CARDSYS`)** so cross-references survive renames and reorganization. This is non-negotiable for AI-first because the agent will rewrite prose freely but should not silently break references.

4. **Single shared "constitution" for project-wide invariants.** Spec Kit's pattern. Avoids N×N duplication of rules across slices. Pairs with a `CLAUDE.md` / `AGENTS.md` that points to it.

5. **Cross-artifact dependencies declared in YAML front-matter** (`dependencies:`, `related:`, `boundedContext:`, `aggregates:`). Lintable. Plain relative-path Markdown links for everything else. **Avoid Obsidian-style `[[wiki-links]]`** — they don't survive renames and agents don't follow them.

6. **Propagation is not solved by tooling — solve it with an agent skill.** Write an explicit instruction in `CLAUDE.md` / `AGENTS.md`: when editing artifact X, re-read all artifacts that reference or are referenced by it via the front-matter graph. This is the single most leveraged piece of automation in the AI-first stack.

7. **Render-time human view, author-time AI-first.** Don't maintain two artifacts. Use Mintlify, Docusaurus, or a custom renderer to hide YAML and style the structure for human consumption.

8. **Progressive disclosure for context economy.** Top-level index (llms.txt-style) → constitution + per-artifact specs → deep `references/`. Keep top-level files ≤300 lines, per-artifact specs ≤500 lines, push detail down.

9. **Acceptance criteria in EARS or Given/When/Then — pick one** and stick with it. Mixing causes friction. EARS parses better; Given/When/Then is more familiar.

10. **Adopt `AGENTS.md` as the cross-tool entry file** with optional tool-specific overlays (`CLAUDE.md`, `.cursor/rules/*.mdc`). It's the converging standard.

The endgame is a repo where each architectural decision, each bounded context, and each aggregate has its own typed Markdown file with front-matter, the relationships are declared in YAML, the agents read the index file first, and a single project skill enforces cross-artifact propagation when one file changes. No platform ships this assembled — but the components are all open, file-based, and AI-friendly.

---

## Source index

### Primary / canonical

- [github/spec-kit](https://github.com/github/spec-kit), [spec-driven.md](https://github.com/github/spec-kit/blob/main/spec-driven.md), [spec-template.md](https://github.com/github/spec-kit/blob/main/templates/spec-template.md), [plan-template.md](https://github.com/github/spec-kit/blob/main/templates/plan-template.md), [tasks-template.md](https://github.com/github/spec-kit/blob/main/templates/tasks-template.md), [constitution-template.md](https://github.com/github/spec-kit/blob/main/templates/constitution-template.md)
- [kiro.dev](https://kiro.dev/), [Kiro specs docs](https://kiro.dev/docs/specs/), [Kiro best practices](https://kiro.dev/docs/specs/best-practices/)
- [llmstxt.org](https://llmstxt.org/), [Answer.AI llms.txt proposal](https://www.answer.ai/posts/2024-09-03-llmstxt.html)
- [Anthropic — Equipping agents for the real world with Agent Skills](https://www.anthropic.com/engineering/equipping-agents-for-the-real-world-with-agent-skills), [Skill authoring best practices](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/best-practices), [anthropics/skills repo](https://github.com/anthropics/skills), [skill-creator SKILL.md](https://github.com/anthropics/skills/blob/main/skills/skill-creator/SKILL.md)
- [Claude Code Memory docs](https://code.claude.com/docs/en/memory), [Cursor Rules docs](https://cursor.com/docs/rules)
- [EARS — Alistair Mavin](https://alistairmavin.com/ears/)
- [Michael Nygard — Documenting Architecture Decisions (2011)](https://www.cognitect.com/blog/2011/11/15/documenting-architecture-decisions), [joelparkerhenderson/architecture-decision-record](https://github.com/joelparkerhenderson/architecture-decision-record), [adr.github.io](https://adr.github.io/)
- [Squarespace RFC template (PDF)](https://engineering.squarespace.com/s/Squarespace-RFC-Template.pdf), [Pragmatic Engineer — RFCs and design docs](https://blog.pragmaticengineer.com/rfcs-and-design-docs/)
- [Working Backwards / PR-FAQ](https://workingbackwards.com/concepts/working-backwards-pr-faq-process/), [Shape Up — Write the Pitch](https://basecamp.com/shapeup/1.5-chapter-06)
- [Diátaxis](https://diataxis.fr/)
- [eugeneyan/ml-design-docs](https://github.com/eugeneyan/ml-design-docs)
- [Backstage — Descriptor Format](https://backstage.io/docs/features/software-catalog/descriptor-format/), [System Model](https://backstage.io/docs/features/software-catalog/system-model/), [Well-known Relations](https://backstage.io/docs/features/software-catalog/well-known-relations/), [Extending the Model](https://backstage.io/docs/features/software-catalog/extending-the-model/)
- [ContextMapper — Bounded Context](https://contextmapper.org/docs/bounded-context/), [Aggregate](https://contextmapper.org/docs/aggregate/), [Customer/Supplier](https://contextmapper.org/docs/customer-supplier/), [Anticorruption Layer](https://contextmapper.org/docs/anticorruption-layer/), [Architectural Refactorings](https://contextmapper.org/docs/architectural-refactorings/), [Tactic DDD Syntax](https://contextmapper.org/docs/tactic-ddd/)
- [Structurizr — Language reference](https://docs.structurizr.com/dsl/language), [Implied relationships](https://docs.structurizr.com/dsl/cookbook/implied-relationships/)
- [arc42 — official template repo](https://github.com/arc42/arc42-template), [Markdown port](https://github.com/NetworkedAssets/arc42-in-markdown-template)
- [DDD-Crew — Bounded Context Canvas](https://github.com/ddd-crew/bounded-context-canvas), [Aggregate Design Canvas](https://github.com/ddd-crew/aggregate-design-canvas), [Markdown BCC port](https://github.com/grjsmith/bounded_context_canvas_md)
- [adr-tools](https://github.com/npryce/adr-tools)
- [Addy Osmani — How to write a good spec for AI agents](https://addyosmani.com/blog/good-spec/)

### Commentary / secondary

- [Mintlify — Docs as AI Interface](https://www.mintlify.com/blog/docs-as-ai-interface), [Best llms.txt platforms 2026](https://www.mintlify.com/library/best-llms-txt-platforms)
- [HumanLayer — Writing a good CLAUDE.md](https://www.humanlayer.dev/blog/writing-a-good-claude-md)
- [Simon Willison — Claude Skills](https://simonwillison.net/2025/Oct/16/claude-skills/)
- [Fern — Docs linting guide](https://buildwithfern.com/post/docs-linting-guide)
- [repowise-dev/repowise](https://github.com/repowise-dev/repowise)
- [ClickHelp — Documentation 2026](https://clickhelp.com/clickhelp-technical-writing-blog/documentation-2026-from-human-centric-to-ai-first/)
- [Document360 — AI documentation trends 2026](https://document360.com/blog/ai-documentation-trends/)
- [GitHub Docs — YAML frontmatter](https://docs.github.com/en/contributing/writing-for-github-docs/using-yaml-frontmatter)
- [PatrickJS/awesome-cursorrules](https://github.com/PatrickJS/awesome-cursorrules)

### Caveat

A handful of WebFetch attempts to llmstxt.org, kiro.dev, and addyosmani.com returned 403 during research; that content was reconstructed from search snippets and other sources that quote them. The Spec Kit, Anthropic Skills, ContextMapper, and DDD-Crew templates were retrieved from their canonical repos.
