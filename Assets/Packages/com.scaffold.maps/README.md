# com.scaffold.maps

# Scaffold Tools Maps

## TL;DR

- Purpose: composite-key map with dynamic predicate indexers.
- Location: `Assets/Packages/com.scaffold.maps/`.
- Depends on: none (BCL only).
- Used by: modules requiring indexed grouped lookups.
- Runtime/Editor: runtime with samples/tests.
- Keywords: map, indexer, composite key, filtered views.

## Responsibilities

- Owns two-key map storage and retrieval.
- Owns dynamic indexer registration by **key** predicates (`AddIndexer`). Predicates inspect primary/secondary only — stored values never reclassify indexer membership.

## Contracts

| Topic | Detail |
|---|---|
| Indexer predicates | **Key-only**. Changing `map[index]` does not move the key in/out of an indexer set; `Values` still reflects live values under those keys. |
| `Indexer.Values` | `IndexerValuesView<>` implementing `IReadOnlyCollection<>`. **`Values.Count`** is O(1) and allocation-free after the Phase 2 storage change. Enumeration walks tracked keys — no snapshot `List<T>` per read. |
| `IReadOnlyMap.TryGetIndexer` | Returns **`IReadOnlyIndexer<>`**. Use `map.AddIndexer` when you need the concrete `Indexer<>`. |

## Public API

| Symbol | Purpose | Inputs | Outputs | Failure behavior |
|---|---|---|---|---|
| `Map<TPrimary,TSecondary,TValue>` | Composite-key value store | keys + value | get/set by pair and indexer support | missing keys follow map semantics (guard/not found) |
| `Indexer<TPrimary,TSecondary,TValue>` | Predicate-based filtered **key** view | key predicate | `IndexerValuesView` | empty when no matching keys |
| `IReadOnlyIndexer<>` | Read-only indexer surface (`Name`, `Count`, `Values`) | via `TryGetIndexer` | view | … |
| `Index<TPrimary,TSecondary>` | Composite key index struct | primary + secondary keys | stable hash/equality key | n/a |
| `BaseMap<TKey,TValue>` | Base map abstraction | generic key/value | base storage behavior | n/a |

## Setup / Integration

1. Reference `Scaffold.Maps`.
2. Create `Map<TPrimary,TSecondary,TValue>`.
3. Register optional indexers for grouped slices.
4. Use indexers to read filtered values without manual sync code.

## How to Use

1. Add entries with primary/secondary keys.
2. Register named indexer predicates.
3. Read from map directly or from indexer views.
4. Remove/clear entries — indexers drop keys accordingly.

## Examples

### Tracking Flow

```mermaid
sequenceDiagram
  participant Caller as Caller
  participant Map as Map<TP,TS,TV>
  participant Idx as Indexer<TP,TS,TV>

  Caller->>Map: Add(primary, secondary, value)
  Map->>Idx: Track(composite index)
  Caller->>Map: Update(existing key, value)
  Map->>Idx: membership unchanged — value updated in place
  Caller->>Map: Remove(primary, secondary)
  Map->>Idx: Untrack(index)
```

### Minimal

```csharp
Map<string, int, string> map = new Map<string, int, string>();
map.Add("Matheus", 29, "Matheus-29");
Indexer<string, int, string> adults = map.AddIndexer("Adults", (name, age) => age >= 18);
IReadOnlyCollection<string> values = adults.Values;
```

## Best Practices

- Keep indexer predicates deterministic and side-effect free.
- Use explicit indexer names for debugging clarity.
- Prefer map/indexer APIs over duplicating filtered caches.

## Anti-Patterns

- Mutating predicate logic based on external unstable state.
- Rebuilding manual mirrored collections on each change.
- Using map as global mutable bag without clear ownership.

## Testing

- Test assembly: `Scaffold.Maps.Tests`.
- Run from repo root:

```powershell
& ".\.agents\scripts\run-editmode-tests.ps1" -AssemblyNames "Scaffold.Maps.Tests"
```

- Expected: all tests pass with zero failures.
- Bugfix rule: add/update regression test first, verify fail-before/fix/pass-after.

## AI Agent Context

- Invariants:
  - index equality/hash remains stable.
  - indexer membership reflects key predicate, not value-only updates.
  - remove/clear operations fully untrack entries.
- Allowed Dependencies:
  - BCL / Unity engine only (no scaffold package required for maps).
- Forbidden Dependencies:
  - module-specific app logic or UI concerns.
- Change Checklist:
  - verify add/update/remove/clear tests.
  - verify indexer auto-tracking tests.
- Known Tricky Areas:
  - updating existing keys and preserving membership behavior.

## Related

- `../../../Architecture.md`
- `../com.scaffold.types/README.md`
- `../com.scaffold.records/README.md`

## Changelog

- Rewritten to AI-first standard with map/indexer tracking sequence diagram.
