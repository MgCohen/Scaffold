using Scaffold.States;

namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Marks a disconnected player as forfeited after reconnect grace expiry.
    /// </summary>
    public class MarkPlayerForfeitedMutator : Mutator<DedicatedServerMatchState>
    {
        private readonly string playerId;
        private readonly long nowUtcTicks;

        public MarkPlayerForfeitedMutator(string playerId, long nowUtcTicks)
        {
            this.playerId = playerId;
            this.nowUtcTicks = nowUtcTicks;
        }

        public override DedicatedServerMatchState Change(DedicatedServerMatchState state)
        {
            var updatedState = state;
            var hasPlayer = DedicatedServerMatchStateEvaluator.TryGetPlayerIndexById(state.Players, playerId, out var playerIndex);
            if (hasPlayer)
            {
                var player = state.Players[playerIndex];
                var forfeitedPlayer = player with { ClientId = 0, HasConnection = false, SnapshotSynced = false, Ready = false, Status = PlayerSessionStatus.Forfeited, LastSeenUtcTicks = nowUtcTicks, ReconnectDeadlineUtcTicks = 0 };
                var updatedPlayers = DedicatedServerMatchStateEvaluator.ReplacePlayer(state.Players, playerIndex, forfeitedPlayer);
                var stage = DedicatedServerMatchStateEvaluator.ResolveStage(updatedPlayers, state.HasStarted);
                updatedState = state with { Players = updatedPlayers, Stage = stage };
            }
            return updatedState;
        }
    }
}
