# Command System Conversation Compact

## Scope

Compact record of the command-system work done in this thread, including progress, findings, and current state.

## Progress Timeline

1. **Plan created (docs only)**
   - Added: `Docs/Plans/CommandSystemPlan.md`
   - Commit: `09e000d`
   - Notes:
     - Initial architecture for command service, metadata, ordering, and transport abstraction.
     - Stream-key rule updated to sender-only (`SourceType + SourceId`) during planning.

2. **First implementation pass (command ordering in command service)**
   - Added command contracts/models/service in `Infra/NetworkMessages` initially.
   - Commit: `41f1092`
   - Notes:
     - Implemented non-static command service with send/subscribe and sender-ordered buffering.

3. **Separation refactor (dedicated command module)**
   - Moved command system into `Assets/Scripts/Infra/Commands`.
   - Removed command-specific code from `Infra/NetworkMessages`.
   - Updated infra dependency tree docs.
   - Commit: `fff8f7c`

4. **Behavior correction based on user feedback**
   - User clarified: **only network messages** should care about sender ordering/buffering.
   - Commands should not enforce sender ordering.
   - Implemented ordering/recovery in `NetworkMessageDispatcher` with resend-request path + cache.
   - Simplified command service to immediate dispatch.
   - Commit: `b25d093`

## Findings

1. **Correct ownership of ordering logic**
   - Ordering/recovery belongs to `NetworkMessages` transport layer, not `Commands`.
   - `Commands` should be transport-agnostic and avoid sender/sequence concerns.

2. **Metadata ownership**
   - Command caller should not provide metadata.
   - Metadata is generated internally by the command service for listener context.

3. **Separation of concerns**
   - `Scaffold.Commands` is now independent of `Scaffold.NetworkMessages`.
   - `NetworkMessages` can handle non-command traffic without inheriting command semantics.

4. **Subscription behavior**
   - Command service supports:
     - typed subscriptions
     - base-type polymorphic subscriptions (`IsAssignableFrom`)
     - subscribe-to-any message listeners

## Current State Summary

### Commands (`Scaffold.Commands`)

- Module path: `Assets/Scripts/Infra/Commands/Runtime`
- API:
  - `ICommand`
  - `ICommandService`
  - `ICommandTransport`
- Behavior:
  - `Send` auto-generates message metadata identifiers/timestamps internally.
  - Incoming messages dispatch immediately (no sender queue ordering).
  - Supports `Subscribe<T>`, `SubscribeAny`, and base-type listener matching.

### NetworkMessages (`Scaffold.NetworkMessages`)

- Module path: `Assets/Scripts/Infra/NetworkMessages/Runtime`
- Dispatcher now includes:
  - outbound sequence assignment
  - inbound sender-ordered buffering
  - missing-sequence request utility (`__scaffold_missing_sequence_request__`)
  - bounded outbound resend cache (`MaxCachedMessages = 1024`)
  - resend on explicit missing-sequence request

## Key Decisions Captured

- **Commands do not own sender ordering.**
- **NetworkMessages owns sender queueing and gap recovery.**
- **Command API remains simple and transport-agnostic.**
- **Subscription model supports any-message + base-type hierarchy.**

## Optional Next Follow-Ups

1. Add explicit tests for:
   - out-of-order receive -> request missing -> reorder flush in `NetworkMessageDispatcher`
   - resend cache hit/miss behavior
2. Add a concrete adapter sample showing `NetworkMessages` wired into `ICommandTransport`.
3. Add diagnostics counters (buffer size, pending gaps, resend attempts).
