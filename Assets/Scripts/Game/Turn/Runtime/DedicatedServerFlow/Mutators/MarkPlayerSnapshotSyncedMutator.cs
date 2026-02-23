using Scaffold.States;

namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Marks a connected player as synchronized to the current authoritative snapshot.
    /// </summary>
    public class MarkPlayerSnapshotSyncedMutator : Mutator<DedicatedServerMatchState>
    {
        private readonly string playerId;
        private readonly int snapshotVersion;
        private readonly long nowUtcTicks;

        public MarkPlayerSnapshotSyncedMutator(string playerId, int snapshotVersion, long nowUtcTicks)
        {
            this.playerId = playerId;
            this.snapshotVersion = snapshotVersion;
            this.nowUtcTicks = nowUtcTicks;
        }

        public override DedicatedServerMatchState Change(DedicatedServerMatchState state)
        {
            var updatedState = state;
            var hasPlayer = DedicatedServerMatchStateEvaluator.TryGetPlayerIndexById(state.Players, playerId, out var playerIndex);
            if (hasPlayer)
            {
                var player = state.Players[playerIndex];
                var syncedPlayer = player with { SnapshotSynced = true, Status = PlayerSessionStatus.SnapshotSynced, LastSeenUtcTicks = nowUtcTicks, LastAcknowledgedSnapshotVersion = snapshotVersion };
                var updatedPlayers = DedicatedServerMatchStateEvaluator.ReplacePlayer(state.Players, playerIndex, syncedPlayer);
                var stage = DedicatedServerMatchStateEvaluator.ResolveStage(updatedPlayers, state.HasStarted);
                updatedState = state with { Players = updatedPlayers, Stage = stage };
            }
            return updatedState;
        }
    }
}
