namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Thin coordinator that maps transport and runtime callbacks into dedicated server flow operations.
    /// </summary>
    public class DedicatedServerMatchCoordinator
    {
        private readonly IDedicatedServerMatchFlowService flowService;
        private readonly string matchId;
        private readonly int protocolVersion;

        public DedicatedServerMatchCoordinator(IDedicatedServerMatchFlowService flowService, string matchId, int protocolVersion)
        {
            this.flowService = flowService;
            this.matchId = matchId;
            this.protocolVersion = protocolVersion;
        }

        public MatchFlowActionResult OnPlayerConnected(string playerId, ulong clientId, long nowUtcTicks)
        {
            var result = flowService.TryConnect(matchId, playerId, clientId, protocolVersion, nowUtcTicks);
            return result;
        }

        public MatchFlowActionResult OnSnapshotAcknowledged(string playerId, int snapshotVersion, int snapshotHash, long nowUtcTicks)
        {
            var result = flowService.AcknowledgeSnapshot(playerId, snapshotVersion, snapshotHash, nowUtcTicks);
            return result;
        }

        public MatchFlowActionResult OnPlayerReady(string playerId, long nowUtcTicks)
        {
            var result = flowService.MarkReady(playerId, nowUtcTicks);
            return result;
        }

        public MatchFlowActionResult OnClientDisconnected(ulong clientId, long nowUtcTicks)
        {
            var result = flowService.HandleDisconnect(clientId, nowUtcTicks);
            return result;
        }

        public MatchFlowActionResult OnPlayerReconnect(string playerId, ulong clientId, long nowUtcTicks)
        {
            var result = flowService.TryReconnect(matchId, playerId, clientId, protocolVersion, nowUtcTicks);
            return result;
        }

        public MatchFlowActionResult OnReconnectGraceTick(long nowUtcTicks)
        {
            var result = flowService.ExpireReconnectWindows(nowUtcTicks);
            return result;
        }

        public void OnSnapshotCheckpointUpdated(int snapshotVersion, int snapshotHash)
        {
            flowService.UpdateSnapshotCheckpoint(snapshotVersion, snapshotHash);
        }

        public DedicatedServerMatchState GetCurrentState()
        {
            var state = flowService.GetState();
            return state;
        }
    }
}
