# Module Documentation Standard (AI-First)

## Why this standard exists

This repository needs module docs that are:

- useful for humans onboarding quickly
- precise for implementation and maintenance
- structured for AI agents to consume reliably

This standard defines one required shape for every module document under `Docs/`.

## Required section order

Every module doc must use this exact top-level order:

1. `# <Module Name>`
2. `## TL;DR`
3. `## Responsibilities`
4. `## Public API`
5. `## Setup / Integration`
6. `## How to Use`
7. `## Examples`
8. `## Best Practices`
9. `## Anti-Patterns`
10. `## Testing`
11. `## AI Agent Context`
12. `## Related`
13. `## Changelog`

## Section intent and rules

### TL;DR

- 3-6 bullets max.
- Include what the module does, where it lives, and who depends on it.

### Responsibilities

- List what the module owns.
- Explicitly list what it must not own.
- Include boundaries (Unity-facing vs pure C#, runtime vs editor).

### Public API

- Only stable, consumer-facing contracts/types.
- Use small tables:
  - symbol
  - purpose
  - inputs
  - outputs
  - failure/edge behavior
- Do not list internal helper types.

### Setup / Integration

- State required dependencies (`.asmdef`, DI installer, configs).
- Include one minimal integration path.
- Include common setup mistakes and fast checks.

### How to Use

- Task-oriented steps.
- Each step should map to a user goal (not implementation internals).

### Examples

- Provide at least:
  - one minimal example
  - one realistic example
  - one error/guard example
- Keep snippets copy-pastable and bounded.

### Best Practices

- 5-10 actionable bullets.
- Reference analyzer expectations and architectural constraints.

### Anti-Patterns

- Show what not to do and why.
- Include migration guidance to preferred patterns.

### Testing

- Required:
  - module test assemblies
  - direct test command(s)
  - expected pass signal
  - regression-test requirement for bug fixes
- Prefer repository scripts over machine-specific Unity CLI examples.

### AI Agent Context

This section is mandatory and optimized for AI-assisted coding:

- `Invariants`: constraints that must never break.
- `Allowed Dependencies`: explicit allowed module dependencies.
- `Forbidden Dependencies`: explicit disallowed module dependencies.
- `Change Checklist`: short list AI agents should execute before finishing.
- `Known Tricky Areas`: places where regressions are common.

### Related

- Link only real files.
- No broken or speculative links.

### Changelog

- Keep a short dated log:
  - date (`YYYY-MM-DD`)
  - what changed in doc
  - reason

## Writing style rules

- Prefer short paragraphs and scan-friendly bullets.
- Use consistent terminology per module.
- Front-load constraints and decisions.
- Keep examples concrete and executable.
- Avoid duplicating global process docs; link to `Docs/Testing.md` and `Architecture.md`.

## AI-first formatting rules

- Use predictable heading names (exact names above).
- Add short keyword lines where useful, for retrieval:
  - `Keywords: navigation, transitions, stack, middleware`
- Keep one fact per bullet when possible.
- Prefer explicit values over vague language (`CloseAllViews=true` instead of "may close screens").

## Module doc template

````md
# <Module Name>

## TL;DR
- Purpose:
- Location:
- Depends on:
- Used by:
- Runtime/Editor:

## Responsibilities
- Owns:
- Does not own:
- Boundaries:

## Public API
| Symbol | Purpose | Inputs | Outputs | Failure behavior |
|---|---|---|---|---|
| `IModuleService.DoThing(...)` | ... | ... | ... | ... |

## Setup / Integration
1. Add dependencies: ...
2. Register installer: ...
3. Validate setup: ...

## How to Use
1. ...
2. ...
3. ...

## Examples
### Minimal
```csharp
// ...
```

### Realistic
```csharp
// ...
```

### Guard / Error path
```csharp
// ...
```

## Best Practices
- ...

## Anti-Patterns
- ...

## Testing
- Test assembly: `...`
- Run:
```powershell
& ".\.agents\scripts\run-editmode-tests.ps1" -AssemblyNames "..."
```
- Expected: all tests pass, zero failures.
- Bugfix rule: add/update regression test first.

## AI Agent Context
- Invariants:
  - ...
- Allowed Dependencies:
  - ...
- Forbidden Dependencies:
  - ...
- Change Checklist:
  - ...
- Known Tricky Areas:
  - ...

## Related
- `Architecture.md`
- `Docs/Testing.md`

## Changelog
- Initial standard created for repo-wide doc remediation.
````

## External references

- Diataxis (documentation structure): [diataxis.fr](https://diataxis.fr/)
- Google developer documentation style (samples and clarity): [developers.google.com/style](https://developers.google.com/style)
- Microsoft technical writing guidance: [learn.microsoft.com/style-guide](https://learn.microsoft.com/en-us/style-guide/welcome/)
- Write the Docs guide: [writethedocs.org/guide](https://www.writethedocs.org/guide/)
- OpenAI prompt engineering guide (for AI-consumable instructions): [platform.openai.com/docs/guides/prompt-engineering](https://platform.openai.com/docs/guides/prompt-engineering)
