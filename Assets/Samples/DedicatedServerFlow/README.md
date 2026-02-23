# Dedicated Server Flow Sample

Basic usage of the dedicated server match flow API.

## Usage

- **`DedicatedServerFlowSample.RunBasicFlow()`** — Builds a Store and `IDedicatedServerMatchFlowService`, initializes a match with two players, and returns the service.
- **`DedicatedServerFlowSample.RunWithCoordinator()`** — Same as above but returns a `DedicatedServerMatchCoordinator` that maps transport/runtime callbacks into flow operations.
- **`ConnectPlayer`**, **`AckSnapshot`**, **`MarkPlayerReady`** — Helper methods that call the coordinator for connect, snapshot ack, and ready.

## Flow

1. Build store and service (or coordinator).
2. Call `InitializeMatch(matchId, protocolVersion, snapshotVersion, snapshotHash, reconnectGraceTicks, playerIds)`.
3. For each connecting player: `TryConnect` (or coordinator `OnPlayerConnected`).
4. Update snapshot checkpoint when ready; then for each player: `AcknowledgeSnapshot`.
5. For each player: `MarkReady`. When all are ready, stage transitions to `InProgress`.

See `DedicatedServerMatchFlowService` and `DedicatedServerMatchCoordinator` in `Assets/Scripts/Game/Turn/Runtime/DedicatedServerFlow/` for the full API.
