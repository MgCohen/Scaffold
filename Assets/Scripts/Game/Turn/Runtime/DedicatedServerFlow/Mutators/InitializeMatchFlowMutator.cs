using System.Collections.Generic;
using Scaffold.States;

namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Creates a fresh authoritative match flow state after matchmaking assignment.
    /// </summary>
    public class InitializeMatchFlowMutator : Mutator<DedicatedServerMatchState>
    {
        private readonly string matchId;
        private readonly int protocolVersion;
        private readonly int snapshotVersion;
        private readonly int snapshotHash;
        private readonly long reconnectGraceTicks;
        private readonly IReadOnlyList<string> playerIds;

        public InitializeMatchFlowMutator(string matchId, int protocolVersion, int snapshotVersion, int snapshotHash, long reconnectGraceTicks, IReadOnlyList<string> playerIds)
        {
            this.matchId = matchId;
            this.protocolVersion = protocolVersion;
            this.snapshotVersion = snapshotVersion;
            this.snapshotHash = snapshotHash;
            this.reconnectGraceTicks = reconnectGraceTicks;
            this.playerIds = playerIds;
        }

        public override DedicatedServerMatchState Change(DedicatedServerMatchState state)
        {
            var players = CreatePlayers();
            var hasStarted = false;
            var stage = DedicatedServerMatchStateEvaluator.ResolveStage(players, hasStarted);
            var initializedState = new DedicatedServerMatchState(matchId, protocolVersion, stage, snapshotVersion, snapshotHash, reconnectGraceTicks, hasStarted, players);
            return initializedState;
        }

        private IReadOnlyList<PlayerSessionState> CreatePlayers()
        {
            var players = new List<PlayerSessionState>();
            for (var index = 0; index < playerIds.Count; index++)
            {
                var playerId = playerIds[index];
                var player = new PlayerSessionState(playerId, 0, false, false, false, PlayerSessionStatus.Assigned, 0, 0, 0);
                players.Add(player);
            }
            return players;
        }
    }
}
