using Scaffold.States;

namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Registers or rebinds a player's active transport client connection.
    /// </summary>
    public class RegisterPlayerConnectionMutator : Mutator<DedicatedServerMatchState>
    {
        private readonly string playerId;
        private readonly ulong clientId;
        private readonly long nowUtcTicks;

        public RegisterPlayerConnectionMutator(string playerId, ulong clientId, long nowUtcTicks)
        {
            this.playerId = playerId;
            this.clientId = clientId;
            this.nowUtcTicks = nowUtcTicks;
        }

        public override DedicatedServerMatchState Change(DedicatedServerMatchState state)
        {
            var updatedState = state;
            var hasPlayer = DedicatedServerMatchStateEvaluator.TryGetPlayerIndexById(state.Players, playerId, out var playerIndex);
            if (hasPlayer)
            {
                var player = state.Players[playerIndex];
                var connectedPlayer = player with { ClientId = clientId, HasConnection = true, SnapshotSynced = false, Ready = false, Status = PlayerSessionStatus.Connected, LastSeenUtcTicks = nowUtcTicks, ReconnectDeadlineUtcTicks = 0, LastAcknowledgedSnapshotVersion = 0 };
                var updatedPlayers = DedicatedServerMatchStateEvaluator.ReplacePlayer(state.Players, playerIndex, connectedPlayer);
                var stage = DedicatedServerMatchStateEvaluator.ResolveStage(updatedPlayers, state.HasStarted);
                updatedState = state with { Players = updatedPlayers, Stage = stage };
            }
            return updatedState;
        }
    }
}
