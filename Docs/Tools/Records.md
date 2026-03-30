# Scaffold Tools Records

## TL;DR

- Purpose: compatibility shim for `init` accessors in target environments needing `IsExternalInit`.
- Location: `Assets/Scripts/Tools/Records/`.
- Depends on: base runtime compiler services only.
- Used by: modules using record/init-style initialization semantics.
- Runtime/Editor: runtime utility with samples/tests.
- Keywords: isexternalinit, init accessor, compatibility.

## Responsibilities

- Owns `System.Runtime.CompilerServices.IsExternalInit` compatibility type.
- Enables `init` property syntax where required by target profile.
- Does not own business models or serialization logic.

## Public API

| Symbol | Purpose | Inputs | Outputs | Failure behavior |
|---|---|---|---|---|
| `IsExternalInit` | Compiler compatibility marker for `init` support | none | compile-time/runtime compatibility type | n/a |

## Setup / Integration

1. Reference `Scaffold.Records` in asmdef using `init` accessors.
2. Keep shim present exactly once across loaded assemblies.

## How to Use

1. Define `init`-only properties in your models.
2. Initialize objects via object initializers.
3. Keep this module referenced for compatibility scenarios.

## Examples

### Minimal

```csharp
private struct Settings
{
    public string Mode { get; init; }
}

Settings s = new Settings { Mode = "Fast" };
```

## Best Practices

- Keep this module minimal and focused.
- Avoid duplicating `IsExternalInit` definitions in other modules.
- Use this as compatibility plumbing, not feature storage.

## Anti-Patterns

- Adding unrelated helper utilities to this module.
- Defining multiple conflicting `IsExternalInit` types.

## Testing

- Test assembly: `Scaffold.Records.Tests`.
- Run from repo root:

```powershell
& ".\.agents\scripts\run-editmode-tests.ps1" -AssemblyNames "Scaffold.Records.Tests"
```

- Expected: all tests pass with zero failures.
- Bugfix rule: add/update regression test first, verify fail-before/fix/pass-after.

## Testing Exception (Intentional Low-Test Module)

This module is an approved low-test exception.

- `Scaffold.Records` is a marker/compatibility assembly whose runtime behavior is intentionally minimal.
- The existing smoke test (`RecordsTests`) is the contract check for `init` assignment compatibility.
- Additional tests are not required unless module scope changes.

Add more tests if any of the following happens:

- New runtime behavior is introduced beyond the `IsExternalInit` compatibility marker.
- Additional public APIs are added under `Assets/Scripts/Tools/Records/`.
- The module begins to own branching logic, state transitions, or domain rules.

See also: `Docs/AutomatedTesting.md` (allowed low-test module exceptions policy).

## AI Agent Context

- Invariants:
  - compatibility shim remains present and unchanged.
- Allowed Dependencies:
  - core runtime compiler namespace only.
- Forbidden Dependencies:
  - infra/app/module-specific logic.
- Change Checklist:
  - verify `init` usage tests still pass.
  - verify no duplicate shim added elsewhere.
- Known Tricky Areas:
  - duplicate type definitions across assemblies.

## Related

- `Architecture.md`
- `Docs/AutomatedTesting.md`
- `Docs/Tools/Types.md`
- `Docs/Tools/Maps.md`

## Changelog

- Rewritten to AI-first standard with explicit compatibility boundaries.
- Added low-test-module exception policy linkage.
