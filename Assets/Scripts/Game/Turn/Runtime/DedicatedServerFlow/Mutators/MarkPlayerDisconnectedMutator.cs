using Scaffold.States;

namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Marks a player as disconnected and opens a reconnect grace window.
    /// </summary>
    public class MarkPlayerDisconnectedMutator : Mutator<DedicatedServerMatchState>
    {
        private readonly string playerId;
        private readonly long nowUtcTicks;
        private readonly long reconnectDeadlineUtcTicks;

        public MarkPlayerDisconnectedMutator(string playerId, long nowUtcTicks, long reconnectDeadlineUtcTicks)
        {
            this.playerId = playerId;
            this.nowUtcTicks = nowUtcTicks;
            this.reconnectDeadlineUtcTicks = reconnectDeadlineUtcTicks;
        }

        public override DedicatedServerMatchState Change(DedicatedServerMatchState state)
        {
            var updatedState = state;
            var hasPlayer = DedicatedServerMatchStateEvaluator.TryGetPlayerIndexById(state.Players, playerId, out var playerIndex);
            if (hasPlayer)
            {
                var player = state.Players[playerIndex];
                var disconnectedPlayer = player with { ClientId = 0, HasConnection = false, SnapshotSynced = false, Ready = false, Status = PlayerSessionStatus.Disconnected, LastSeenUtcTicks = nowUtcTicks, ReconnectDeadlineUtcTicks = reconnectDeadlineUtcTicks };
                var updatedPlayers = DedicatedServerMatchStateEvaluator.ReplacePlayer(state.Players, playerIndex, disconnectedPlayer);
                var stage = DedicatedServerMatchStateEvaluator.ResolveStage(updatedPlayers, state.HasStarted);
                updatedState = state with { Players = updatedPlayers, Stage = stage };
            }
            return updatedState;
        }
    }
}
