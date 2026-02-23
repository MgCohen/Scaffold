# Dedicated Server Turn-Based Game Flow Plan (Post-Matchmaking)

## Goal

Define a server-authoritative post-matchmaking flow for turn-based multiplayer matches with clear handling for:

- connection
- initial state synchronization
- player readiness gating
- connection/disconnection events
- reconnection

## Design principles

1. Dedicated server is the single source of truth for game state.
2. Player identity is persistent (`PlayerId`) and independent from transport `ClientId`.
3. Commands are validated against authoritative turn/state version on the server.
4. Rejoin uses full snapshot or delta replay from server state.
5. Match and player lifecycle are explicit state machines.

## Match lifecycle

- `Allocated`
- `AwaitingConnections`
- `AwaitingSnapshotSync`
- `AwaitingReady`
- `InProgress`
- `Completed`
- `Cancelled`

## Player lifecycle

- `Assigned`
- `Connected`
- `SnapshotSynced`
- `Ready`
- `Disconnected`
- `Forfeited`

## Post-matchmaking flow

### 1) Connect

1. Matchmaker returns `matchId`, `playerId`, server endpoint, join token, protocol version.
2. Client connects and sends join payload.
3. Server validates token/session, match membership, protocol version, and seat ownership.
4. Server maps `playerId -> clientId` and transitions player to `Connected`.

### 2) Initial state sync

1. Server prepares authoritative snapshot descriptor (`snapshotVersion`, `snapshotHash`).
2. Server sends snapshot payload to connected clients.
3. Client applies snapshot and sends ack with version/hash.
4. Server validates ack and transitions player to `SnapshotSynced`.
5. Match transitions to `AwaitingReady` once all players are `SnapshotSynced`.

### 3) Ready gate

1. Client sends ready signal only after snapshot apply and local scene/UI initialization.
2. Server validates ready eligibility (`Connected` and `SnapshotSynced`).
3. Server transitions player to `Ready`.
4. Match transitions to `InProgress` once all required players are `Ready`.

## Connection/disconnection handling

### Disconnect event

1. Server receives disconnect callback/timeout event.
2. Server transitions player to `Disconnected`.
3. Server sets `reconnectDeadlineUtcTicks = disconnectTime + reconnectGrace`.
4. Server clears readiness/sync flags to force revalidation on rejoin.

### Grace window expiry

1. Server periodically checks disconnected players against reconnect deadline.
2. Expired players transition to `Forfeited`.
3. If forfeiture occurs before match start, match can transition to `Cancelled`.
4. If forfeiture occurs during `InProgress`, game policy decides bot replacement/auto-loss/end state.

## Reconnection handling

### Reconnect event

1. Client reconnects with `matchId`, `playerId`, protocol version, reconnect token/session.
2. Server validates identity, match, protocol, and reconnect deadline.
3. Server remaps `playerId -> newClientId`.
4. Player transitions to `Connected`.
5. Server sends current authoritative snapshot.
6. Client re-acks snapshot and sends ready again.

### Reconnect guarantees

- Commands are deduplicated with command IDs.
- Server rejects stale commands by expected turn/version.
- Reconnected player never regains authority from client-local assumptions; server state always wins.

## Scaffold-aligned implementation plan

1. Represent flow with Store state slices (`DedicatedServerMatchState`, `PlayerSessionState`).
2. Apply changes with explicit mutators:
   - initialize match
   - connect
   - snapshot ack
   - ready
   - disconnect
   - reconnect
   - forfeit
   - snapshot descriptor update
3. Add orchestration service (`DedicatedServerMatchFlowService`) that:
   - validates requests
   - executes mutators
   - returns typed result codes for networking layer
4. Keep networking transport callbacks thin and route all business transitions through service methods.

## Operational defaults (initial recommendation)

- Reconnect grace: `120` seconds
- Snapshot ack timeout before ready: `30` seconds
- Ready timeout pre-game: `45` seconds
- Connection approval timeout: transport default + explicit server reason codes

## Validation and observability

- Emit structured logs for every state transition with `matchId`, `playerId`, old/new status, and reason.
- Track metrics:
  - connect success/failure by reason
  - snapshot ack failures
  - ready wait duration
  - reconnect attempts, success ratio, and expiry forfeits

## Non-goals for this iteration

- Full transport-level packet reliability implementation
- UI implementation details
- persistence backend schema

