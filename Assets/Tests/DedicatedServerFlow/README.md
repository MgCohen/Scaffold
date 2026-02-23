# Dedicated Server Flow Tests

Edit-mode tests for the dedicated server match flow service.

## Running

Open **Window → General → Test Runner**, select **EditMode**, then run the tests under **Scaffold.Turns.Tests**.

## Test cases

- **GetState_AfterInitialize_ReturnsAwaitingConnections** — State is correct after `InitializeMatch`.
- **TryConnect_ValidPlayer_ReturnsAccepted** — Valid connect is accepted and requires snapshot sync.
- **TryConnect_WrongMatchId_ReturnsMatchMismatch** — Wrong match ID is rejected.
- **TryConnect_UnknownPlayer_ReturnsUnknownPlayer** — Unknown player ID is rejected.
- **TryConnect_ThenAcknowledgeSnapshot_ThenMarkReady_TransitionsCorrectly** — Full flow: connect both players, update checkpoint, ack snapshot, mark ready; stage becomes `InProgress`.
- **AcknowledgeSnapshot_WithoutConnect_ReturnsPlayerNotConnected** — Snapshot ack without prior connect is rejected.
- **MarkReady_WithoutSnapshotSync_ReturnsSnapshotNotSynced** — Ready without snapshot sync is rejected.
- **HandleDisconnect_UnknownClient_ReturnsUnknownClient** — Disconnect for unknown client is rejected.
- **HandleDisconnect_ConnectedPlayer_ReturnsAccepted** — Disconnect for connected player is accepted and sets waiting-for-reconnect.
