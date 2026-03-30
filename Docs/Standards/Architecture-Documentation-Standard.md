# Architecture Documentation Standard (AI-First + Visual-First)

## Purpose

This standard defines how `Architecture.md` and architecture-focused docs should be written in this repository.

Goals:

- fast onboarding for humans
- deterministic context for AI agents
- visual clarity through diagram-as-code

## Required architecture sections

Every architecture document must include, in this order:

1. `## TL;DR`
2. `## Architectural Drivers`
3. `## Constraints and Invariants`
4. `## System Context` (diagram)
5. `## Container/Module View` (diagram)
6. `## Runtime Flows` (sequence/state diagrams)
7. `## Dependency Rules` (allowed/forbidden)
8. `## Quality Attributes and Tradeoffs`
9. `## Verification` (tests/analyzers/checks)
10. `## Change Log`

## Required diagram set

At minimum, maintain these diagrams:

1. System context diagram (external actors and systems).
2. Container/module dependency diagram (assemblies/modules).
3. One sequence diagram per critical user flow.
4. One state diagram for any important runtime lifecycle.

If deployment topology matters, add deployment diagram.

## Diagram notation policy

- Primary format: Mermaid in Markdown (` ```mermaid ` blocks).
- Optional: exported PNG/SVG only as convenience artifacts; Mermaid source remains canonical.
- Preferred visual modeling approach:
  - C4 levels for static structure (Context, Container, Component)
  - UML-like sequence/state/class where flow/detail is needed

## Diagram quality rules

- Every diagram must include a title line above the block.
- Keep labels explicit and domain-specific.
- Use consistent names across all docs (`MainMenuViewController`, not mixed aliases).
- Limit each diagram to one story; split large diagrams.
- Ensure arrows indicate direction of dependency/control clearly.

## AI-first architecture metadata

For each diagram section, add a short metadata block:

- `Intent:` what decision/question this diagram answers
- `Source of truth:` code/doc paths that back the diagram
- `Update trigger:` what code changes require diagram update

## Repository conventions

- Keep architecture docs under `Docs/Documentation/` unless they are module-local.
- Keep module docs under `Docs/<Layer>/<Module>.md`.
- `Architecture.md` is the architecture entrypoint and must link to all deeper architecture docs.

## Recommended references

- C4 model (official): [https://c4model.com/](https://c4model.com/)
- C4 diagram types: [https://c4model.com/diagrams](https://c4model.com/diagrams)
- arc42 overview/template: [https://arc42.org/overview](https://arc42.org/overview)
- Mermaid syntax reference: [https://mermaid.js.org/intro/syntax-reference](https://mermaid.js.org/intro/syntax-reference)
- Mermaid sequence diagrams: [https://mermaid.js.org/syntax/sequenceDiagram](https://mermaid.js.org/syntax/sequenceDiagram)

## Architecture review checklist

- Does each critical flow have a diagram?
- Do dependency arrows match actual `.asmdef`/`.csproj` references?
- Are forbidden dependencies explicitly documented?
- Can a new teammate explain startup and one game loop from docs alone?
- Can an AI agent infer safe edit boundaries from invariants and dependency rules?
