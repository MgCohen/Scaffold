using Scaffold.States;

namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Marks a synchronized player as ready and starts the match when all required players are ready.
    /// </summary>
    public class MarkPlayerReadyMutator : Mutator<DedicatedServerMatchState>
    {
        private readonly string playerId;
        private readonly long nowUtcTicks;

        public MarkPlayerReadyMutator(string playerId, long nowUtcTicks)
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
                var readyPlayer = player with { Ready = true, Status = PlayerSessionStatus.Ready, LastSeenUtcTicks = nowUtcTicks };
                var updatedPlayers = DedicatedServerMatchStateEvaluator.ReplacePlayer(state.Players, playerIndex, readyPlayer);
                var hasStarted = ResolveStartFlag(state.HasStarted, updatedPlayers);
                var stage = DedicatedServerMatchStateEvaluator.ResolveStage(updatedPlayers, hasStarted);
                updatedState = state with { Players = updatedPlayers, HasStarted = hasStarted, Stage = stage };
            }
            return updatedState;
        }

        private bool ResolveStartFlag(bool hasStarted, System.Collections.Generic.IReadOnlyList<PlayerSessionState> players)
        {
            var startFlag = hasStarted;
            var allReady = DedicatedServerMatchStateEvaluator.AreAllPlayersReady(players);
            if (!hasStarted && allReady)
            {
                startFlag = true;
            }
            return startFlag;
        }
    }
}
