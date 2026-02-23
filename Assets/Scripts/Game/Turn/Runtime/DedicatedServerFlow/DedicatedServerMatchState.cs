using System.Collections.Generic;
using Scaffold.States;

namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Immutable authoritative state for post-matchmaking connection, synchronization, readiness, and reconnection flow.
    /// </summary>
    public record DedicatedServerMatchState(string MatchId, int ProtocolVersion, MatchFlowStage Stage, int SnapshotVersion, int SnapshotHash, long ReconnectGraceTicks, bool HasStarted, IReadOnlyList<PlayerSessionState> Players) : State;
}
