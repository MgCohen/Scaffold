using System.Collections.Generic;
using Scaffold.States;

namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Orchestrates authoritative dedicated-server match flow transitions through Store mutators.
    /// </summary>
    public class DedicatedServerMatchFlowService : IDedicatedServerMatchFlowService
    {
        private readonly Store store;

        public DedicatedServerMatchFlowService(Store store)
        {
            this.store = store;
        }

        public void InitializeMatch(string matchId, int protocolVersion, int snapshotVersion, int snapshotHash, long reconnectGraceTicks, IReadOnlyList<string> playerIds)
        {
            var mutator = new InitializeMatchFlowMutator(matchId, protocolVersion, snapshotVersion, snapshotHash, reconnectGraceTicks, playerIds);
            store.Execute(mutator);
        }

        public MatchFlowActionResult TryConnect(string matchId, string playerId, ulong clientId, int protocolVersion, long nowUtcTicks)
        {
            var state = GetState();
            var code = ValidateConnect(state, matchId, playerId, protocolVersion);
            if (code == MatchFlowActionCode.Accepted)
            {
                ApplyConnectMutator(playerId, clientId, nowUtcTicks);
                state = GetState();
            }
            var requiresSnapshotSync = code == MatchFlowActionCode.Accepted;
            var waitingForReconnect = DedicatedServerMatchStateEvaluator.HasDisconnectedPlayer(state.Players);
            var result = BuildResult(code, requiresSnapshotSync, waitingForReconnect, state);
            return result;
        }

        public MatchFlowActionResult AcknowledgeSnapshot(string playerId, int snapshotVersion, int snapshotHash, long nowUtcTicks)
        {
            var state = GetState();
            var code = ValidateSnapshotAck(state, playerId, snapshotVersion, snapshotHash);
            if (code == MatchFlowActionCode.Accepted)
            {
                ApplySnapshotAckMutator(playerId, snapshotVersion, nowUtcTicks);
                state = GetState();
            }
            var waitingForReconnect = DedicatedServerMatchStateEvaluator.HasDisconnectedPlayer(state.Players);
            var result = BuildResult(code, false, waitingForReconnect, state);
            return result;
        }

        public MatchFlowActionResult MarkReady(string playerId, long nowUtcTicks)
        {
            var state = GetState();
            var code = ValidateReady(state, playerId);
            if (code == MatchFlowActionCode.Accepted)
            {
                ApplyReadyMutator(playerId, nowUtcTicks);
                state = GetState();
            }
            var waitingForReconnect = DedicatedServerMatchStateEvaluator.HasDisconnectedPlayer(state.Players);
            var result = BuildResult(code, false, waitingForReconnect, state);
            return result;
        }

        public MatchFlowActionResult HandleDisconnect(ulong clientId, long nowUtcTicks)
        {
            var state = GetState();
            var hasPlayer = TryGetConnectedPlayerByClientId(state, clientId, out var player);
            var code = MatchFlowActionCode.UnknownClient;
            var matchNotInitialized = IsMatchNotInitialized(state);
            if (matchNotInitialized)
            {
                code = MatchFlowActionCode.MatchNotInitialized;
            }
            if (!matchNotInitialized && hasPlayer)
            {
                code = MatchFlowActionCode.Accepted;
                ApplyDisconnectMutator(player.PlayerId, nowUtcTicks, state.ReconnectGraceTicks);
                state = GetState();
            }
            var waitingForReconnect = DedicatedServerMatchStateEvaluator.HasDisconnectedPlayer(state.Players);
            var result = BuildResult(code, false, waitingForReconnect, state);
            return result;
        }

        public MatchFlowActionResult TryReconnect(string matchId, string playerId, ulong clientId, int protocolVersion, long nowUtcTicks)
        {
            var state = GetState();
            var code = ValidateReconnect(state, matchId, playerId, protocolVersion, nowUtcTicks);
            if (code == MatchFlowActionCode.Accepted)
            {
                ApplyConnectMutator(playerId, clientId, nowUtcTicks);
                state = GetState();
            }
            var requiresSnapshotSync = code == MatchFlowActionCode.Accepted;
            var waitingForReconnect = DedicatedServerMatchStateEvaluator.HasDisconnectedPlayer(state.Players);
            var result = BuildResult(code, requiresSnapshotSync, waitingForReconnect, state);
            return result;
        }

        public MatchFlowActionResult ExpireReconnectWindows(long nowUtcTicks)
        {
            var state = GetState();
            var expiredPlayers = DedicatedServerMatchStateEvaluator.GetExpiredDisconnectedPlayers(state.Players, nowUtcTicks);
            var hasExpiredPlayers = expiredPlayers.Count > 0;
            var code = MatchFlowActionCode.NoExpiredConnections;
            var matchNotInitialized = IsMatchNotInitialized(state);
            if (matchNotInitialized)
            {
                code = MatchFlowActionCode.MatchNotInitialized;
            }
            if (!matchNotInitialized && hasExpiredPlayers)
            {
                code = MatchFlowActionCode.Accepted;
                ApplyExpiredForfeitMutators(expiredPlayers, nowUtcTicks);
                state = GetState();
            }
            var waitingForReconnect = DedicatedServerMatchStateEvaluator.HasDisconnectedPlayer(state.Players);
            var result = BuildResult(code, false, waitingForReconnect, state);
            return result;
        }

        public void UpdateSnapshotCheckpoint(int snapshotVersion, int snapshotHash)
        {
            var mutator = new UpdateSnapshotCheckpointMutator(snapshotVersion, snapshotHash);
            store.Execute(mutator);
        }

        private MatchFlowActionCode ValidateConnect(DedicatedServerMatchState state, string matchId, string playerId, int protocolVersion)
        {
            var code = MatchFlowActionCode.Accepted;
            var hasPlayer = TryGetPlayer(state, playerId, out var player);
            if (code == MatchFlowActionCode.Accepted && IsMatchNotInitialized(state))
            {
                code = MatchFlowActionCode.MatchNotInitialized;
            }
            if (code == MatchFlowActionCode.Accepted && state.MatchId != matchId)
            {
                code = MatchFlowActionCode.MatchMismatch;
            }
            if (code == MatchFlowActionCode.Accepted && state.ProtocolVersion != protocolVersion)
            {
                code = MatchFlowActionCode.ProtocolMismatch;
            }
            if (code == MatchFlowActionCode.Accepted && !hasPlayer)
            {
                code = MatchFlowActionCode.UnknownPlayer;
            }
            if (code == MatchFlowActionCode.Accepted && hasPlayer && player.Status == PlayerSessionStatus.Forfeited)
            {
                code = MatchFlowActionCode.PlayerForfeited;
            }
            if (code == MatchFlowActionCode.Accepted && hasPlayer && player.HasConnection)
            {
                code = MatchFlowActionCode.AlreadyConnected;
            }
            return code;
        }

        private void ApplyConnectMutator(string playerId, ulong clientId, long nowUtcTicks)
        {
            var mutator = new RegisterPlayerConnectionMutator(playerId, clientId, nowUtcTicks);
            store.Execute(mutator);
        }

        private MatchFlowActionCode ValidateSnapshotAck(DedicatedServerMatchState state, string playerId, int snapshotVersion, int snapshotHash)
        {
            var code = MatchFlowActionCode.Accepted;
            var hasPlayer = TryGetPlayer(state, playerId, out var player);
            if (code == MatchFlowActionCode.Accepted && IsMatchNotInitialized(state))
            {
                code = MatchFlowActionCode.MatchNotInitialized;
            }
            if (code == MatchFlowActionCode.Accepted && !hasPlayer)
            {
                code = MatchFlowActionCode.UnknownPlayer;
            }
            if (code == MatchFlowActionCode.Accepted && hasPlayer && !player.HasConnection)
            {
                code = MatchFlowActionCode.PlayerNotConnected;
            }
            if (code == MatchFlowActionCode.Accepted && state.SnapshotVersion != snapshotVersion)
            {
                code = MatchFlowActionCode.SnapshotVersionMismatch;
            }
            if (code == MatchFlowActionCode.Accepted && state.SnapshotHash != snapshotHash)
            {
                code = MatchFlowActionCode.SnapshotHashMismatch;
            }
            return code;
        }

        private void ApplySnapshotAckMutator(string playerId, int snapshotVersion, long nowUtcTicks)
        {
            var mutator = new MarkPlayerSnapshotSyncedMutator(playerId, snapshotVersion, nowUtcTicks);
            store.Execute(mutator);
        }

        private MatchFlowActionCode ValidateReady(DedicatedServerMatchState state, string playerId)
        {
            var code = MatchFlowActionCode.Accepted;
            var hasPlayer = TryGetPlayer(state, playerId, out var player);
            if (code == MatchFlowActionCode.Accepted && IsMatchNotInitialized(state))
            {
                code = MatchFlowActionCode.MatchNotInitialized;
            }
            if (code == MatchFlowActionCode.Accepted && !hasPlayer)
            {
                code = MatchFlowActionCode.UnknownPlayer;
            }
            if (code == MatchFlowActionCode.Accepted && hasPlayer && !player.HasConnection)
            {
                code = MatchFlowActionCode.PlayerNotConnected;
            }
            if (code == MatchFlowActionCode.Accepted && hasPlayer && !player.SnapshotSynced)
            {
                code = MatchFlowActionCode.SnapshotNotSynced;
            }
            if (code == MatchFlowActionCode.Accepted && hasPlayer && player.Status == PlayerSessionStatus.Forfeited)
            {
                code = MatchFlowActionCode.PlayerForfeited;
            }
            return code;
        }

        private void ApplyReadyMutator(string playerId, long nowUtcTicks)
        {
            var mutator = new MarkPlayerReadyMutator(playerId, nowUtcTicks);
            store.Execute(mutator);
        }

        private bool TryGetConnectedPlayerByClientId(DedicatedServerMatchState state, ulong clientId, out PlayerSessionState player)
        {
            var hasPlayer = DedicatedServerMatchStateEvaluator.TryGetConnectedPlayerIndexByClientId(state.Players, clientId, out var playerIndex);
            var resolvedPlayer = default(PlayerSessionState);
            if (hasPlayer)
            {
                resolvedPlayer = state.Players[playerIndex];
            }
            player = resolvedPlayer;
            return hasPlayer;
        }

        private void ApplyDisconnectMutator(string playerId, long nowUtcTicks, long reconnectGraceTicks)
        {
            var reconnectDeadlineUtcTicks = nowUtcTicks + reconnectGraceTicks;
            var mutator = new MarkPlayerDisconnectedMutator(playerId, nowUtcTicks, reconnectDeadlineUtcTicks);
            store.Execute(mutator);
        }

        private MatchFlowActionCode ValidateReconnect(DedicatedServerMatchState state, string matchId, string playerId, int protocolVersion, long nowUtcTicks)
        {
            var code = MatchFlowActionCode.Accepted;
            var hasPlayer = TryGetPlayer(state, playerId, out var player);
            if (code == MatchFlowActionCode.Accepted && IsMatchNotInitialized(state))
            {
                code = MatchFlowActionCode.MatchNotInitialized;
            }
            if (code == MatchFlowActionCode.Accepted && state.MatchId != matchId)
            {
                code = MatchFlowActionCode.MatchMismatch;
            }
            if (code == MatchFlowActionCode.Accepted && state.ProtocolVersion != protocolVersion)
            {
                code = MatchFlowActionCode.ProtocolMismatch;
            }
            if (code == MatchFlowActionCode.Accepted && !hasPlayer)
            {
                code = MatchFlowActionCode.UnknownPlayer;
            }
            if (code == MatchFlowActionCode.Accepted && hasPlayer && player.Status == PlayerSessionStatus.Forfeited)
            {
                code = MatchFlowActionCode.PlayerForfeited;
            }
            if (code == MatchFlowActionCode.Accepted && hasPlayer && player.HasConnection)
            {
                code = MatchFlowActionCode.AlreadyConnected;
            }
            if (code == MatchFlowActionCode.Accepted && hasPlayer && player.Status != PlayerSessionStatus.Disconnected)
            {
                code = MatchFlowActionCode.PlayerNotDisconnected;
            }
            if (code == MatchFlowActionCode.Accepted && hasPlayer && nowUtcTicks > player.ReconnectDeadlineUtcTicks)
            {
                code = MatchFlowActionCode.ReconnectWindowExpired;
            }
            return code;
        }

        private void ApplyExpiredForfeitMutators(IReadOnlyList<string> playerIds, long nowUtcTicks)
        {
            for (var index = 0; index < playerIds.Count; index++)
            {
                var playerId = playerIds[index];
                var mutator = new MarkPlayerForfeitedMutator(playerId, nowUtcTicks);
                store.Execute(mutator);
            }
        }

        private MatchFlowActionResult BuildResult(MatchFlowActionCode code, bool requiresSnapshotSync, bool waitingForReconnect, DedicatedServerMatchState state)
        {
            var matchStarted = state.Stage == MatchFlowStage.InProgress;
            var result = new MatchFlowActionResult(code, requiresSnapshotSync, matchStarted, waitingForReconnect);
            return result;
        }

        private bool IsMatchNotInitialized(DedicatedServerMatchState state)
        {
            var isMatchNotInitialized = string.IsNullOrWhiteSpace(state.MatchId);
            return isMatchNotInitialized;
        }

        private bool TryGetPlayer(DedicatedServerMatchState state, string playerId, out PlayerSessionState player)
        {
            var hasPlayer = DedicatedServerMatchStateEvaluator.TryGetPlayerIndexById(state.Players, playerId, out var playerIndex);
            var resolvedPlayer = default(PlayerSessionState);
            if (hasPlayer)
            {
                resolvedPlayer = state.Players[playerIndex];
            }
            player = resolvedPlayer;
            return hasPlayer;
        }

        public DedicatedServerMatchState GetState()
        {
            var state = store.Get<DedicatedServerMatchState>();
            return state;
        }
    }
}
