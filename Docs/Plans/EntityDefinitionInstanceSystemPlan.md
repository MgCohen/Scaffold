# Entity System Plan (Definition + Instance Split)

## Goals

- Keep immutable gameplay configuration in **definitions**.
- Keep mutable runtime state in **instances**.
- Use typed retrieval by runtime type (`Type`) instead of type-id mapping.
- Keep definition IDs and instance IDs in separate domains.
- Keep attribute runtime values deterministic and network-friendly (`int` values).

## Core Model

### Definitions (immutable)

Each definition contains:

- `DefinitionId` (definition domain only)
- Runtime `Type` identity (used for typed lookup)
- Immutable data (name, rules, metadata)
- Base attributes (`AttributeDefinitionId -> int`)

Definitions are never mutated at runtime and can be shared by many instances.

### Instances (mutable)

Each instance contains:

- `InstanceId` (instance domain only)
- `DefinitionId` reference
- `AttributeBag` with:
  - `BaseValues`
  - `CurrentValues`
  - `Tags`
  - `Modifiers`

Instances mutate during play; definitions do not.

## Lookup and Service Structure

Create an `EntitiesService` facade with sibling services:

1. `EntityDefinitionLookup`
   - Stores definitions by `DefinitionId`
   - Stores definitions by `Type`
   - API:
     - `GetDefinition<TDefinition>(definitionId)`

2. `EntityInstanceLookup`
   - Stores instances by `InstanceId`
   - Stores instances by `Type`
   - Stores instance ids grouped by `DefinitionId`
   - API:
     - `GetEntity<TEntity>(instanceId, definitionId)`

3. `EntityFactory`
   - Uses definition to create an instance with seeded base attributes.
   - Registration flow:
     - `definition = GetDefinition<TDefinition>(definitionId)`
     - `entity = definition.CreateInstance(instanceId)`
     - `entity.Initialize(...)` (optional domain init step)
     - `instanceLookup.Register(entity)`

## Attribute Runtime Rules

- Attribute keys and values are integers.
- On changes to base values, tags, or modifiers, recalculate current values.
- Recalculation source of truth:
  - `Current = Base + Ordered(Modifiers)`
- Modifiers are applied in deterministic order:
  - `Priority`, then operation, then modifier instance id.

## ID Domains

Use separate ID types:

- `EntityDefinitionId`
- `EntityInstanceId`
- `AttributeDefinitionId`
- `TagDefinitionId`
- `ModifierDefinitionId`
- `ModifierInstanceId`

No ID type is reused between definition and instance spaces.

## Typed Retrieval API Contract

- `GetDefinition<TDefinition>(definitionId)`:
  - Returns definition if id exists and runtime type matches `TDefinition`.
  - Throws on missing id or type mismatch.

- `GetEntity<TEntity>(instanceId, definitionId)`:
  - Returns instance if id exists, definition id matches, and type matches `TEntity`.
  - Throws on missing id, mismatched definition id, or type mismatch.

## Network Shape (integer payloads)

Runtime messages should carry:

- `instanceId`
- `definitionId`
- Integer attribute/tag/modifier deltas

Type identity remains local runtime behavior (`Type` in lookups), not wire payload.

## Implementation Scope for This Iteration

1. Create base ID/value models.
2. Implement definition and instance abstractions.
3. Implement attribute bag with deterministic recalc.
4. Implement sibling lookup services.
5. Implement `EntitiesService` facade with typed `GetDefinition` and `GetEntity`.
