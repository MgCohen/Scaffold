# Command System Service Plan

## Goal

Build a non-static command service that exposes a simple API to:

- Send command
- Subscribe to specific command type

The service must be transport-agnostic. Network is an implementation detail and must stay behind abstractions.

## Scope

### In scope

- Command service contracts
- Command envelope and metadata model
- Sender-based ordering queues for inbound messages
- Out-of-order buffering and ordered release
- Extensibility points for network and serialization adapters

### Out of scope for now

- Concrete gameplay command types (PlayCard, DealDamage, ChangeHealth, and others)
- Specific network transport wiring
- Specific serialization implementation

## Core design

### 1) Public command API

- `Send(command)`
- `Subscribe<TCommand>(handler)`

Subscription is typed by command payload type. API is not coupled to network or any serialization format.

### 2) Base command contract

Provide one shared base contract for all command payloads:

- Marker interface or abstract base class

No domain-specific command implementations are required in this phase.

### 3) Envelope + metadata layer

All commands must flow through an envelope that contains:

- Command payload
- Metadata

Metadata must include:

- Unique message id
- Source identity
- Sequence number
- Timestamps (created and received)
- Optional correlation fields for tracing

### 4) Stream key rule for ordering

Ordering queues are keyed only by sender identity:

- `StreamKey = (SourceType, SourceId)`

Session, channel, and command type do not partition queues. Everything from one sender is ordered in the same stream.

### 5) Inbound ordering behavior

For each stream:

- Track `ExpectedSequence`
- Keep a pending buffer for future messages

When receiving a message:

- If `sequence == expected`: dispatch immediately, increment expected, then flush consecutive buffered messages
- If `sequence > expected`: store in pending buffer and wait for missing sequences
- If `sequence < expected`: treat as stale/duplicate and ignore

Example:

- Receive sender sequence 1: dispatch 1
- Receive sender sequence 3: hold 3 in pending
- Receive sender sequence 2: dispatch 2, then flush 3

### 6) Service internals

The service contains:

- Subscription registry
- Incoming stream-ordering manager
- Outgoing sequence generator
- Transport adapter port
- Serialization adapter port

### 7) Transport abstraction

Define a transport contract that supports:

- Sending envelope
- Delivering inbound envelopes to the service

The core command service must not reference concrete network implementations.

### 8) Serialization abstraction

Define serializer contract for payload conversion at transport boundary.

This keeps support open for `networkMessages` and `ISerializationStruct` without changing the command API.

## Delivery phases

### Phase 1

- Add contracts and models
- Implement local in-process command service with send and subscribe

### Phase 2

- Implement sender-keyed sequence ordering and pending queues
- Add duplicate/stale handling

### Phase 3

- Add transport and serialization adapter interfaces
- Add optional bridge to existing `NetworkMessages` module

### Phase 4

- Add diagnostics, buffer limits, and timeout policy for stuck gaps
- Add unit tests for ordering and subscription behavior

## Acceptance criteria

- Command service is instance-based, not static
- API supports send and typed subscribe
- Metadata is attached to all command envelopes
- Inbound processing is ordered per sender using `(SourceType, SourceId)` only
- Out-of-order messages are buffered and released in order when gaps are filled
- Network and serialization remain behind abstractions
