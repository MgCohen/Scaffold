using System.Collections.Generic;

namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Authoritative service contract for post-matchmaking dedicated server session flow.
    /// </summary>
    public interface IDedicatedServerMatchFlowService
    {
        void InitializeMatch(string matchId, int protocolVersion, int snapshotVersion, int snapshotHash, long reconnectGraceTicks, IReadOnlyList<string> playerIds);

        MatchFlowActionResult TryConnect(string matchId, string playerId, ulong clientId, int protocolVersion, long nowUtcTicks);

        MatchFlowActionResult AcknowledgeSnapshot(string playerId, int snapshotVersion, int snapshotHash, long nowUtcTicks);

        MatchFlowActionResult MarkReady(string playerId, long nowUtcTicks);

        MatchFlowActionResult HandleDisconnect(ulong clientId, long nowUtcTicks);

        MatchFlowActionResult TryReconnect(string matchId, string playerId, ulong clientId, int protocolVersion, long nowUtcTicks);

        MatchFlowActionResult ExpireReconnectWindows(long nowUtcTicks);

        void UpdateSnapshotCheckpoint(int snapshotVersion, int snapshotHash);

        DedicatedServerMatchState GetState();
    }
}
