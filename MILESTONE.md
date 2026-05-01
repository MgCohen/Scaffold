# Milestone Plans

Use this file as a short guide for writing milestone plan docs referenced by an ExecPlan.

Milestone plan file path:

`Plans/[FeatureName]/milestones/ExecPlan-Milestone-[x].md`

When to create one:

Create a milestone plan when a milestone is too complex for a simple paragraph in the parent ExecPlan. The parent ExecPlan `Progress` item must point to this milestone file.

## Goal

State the milestone objective in 2-4 sentences:

- What problem this milestone solves.
- What should exist after completion that did not exist before.
- How this helps the parent ExecPlan outcome.

## Deliverable

List the concrete outputs expected at the end of the milestone:

- Code/files/modules updated.
- Behavior change (observable result).
- Tests added/updated (include regression tests for bug fixes).

## Plan

Describe the execution sequence in short, concrete steps:

1. Implement the milestone scope.
2. Re-run any added regression test and confirm pass.
3. Run `.agents/scripts/validate-changes.cmd`.
4. Fix failures and re-run until clean.
5. For bug fixes, include/verify a regression test that reproduces the bug.
6. Commit milestone changes.

## Snippets and Samples

Add short examples only when useful:

- Small code snippets for non-obvious changes.
- Sample command lines and expected outputs.
- Tiny before/after examples that prove behavior.

Keep examples concise and focused on verification, not full implementations.
