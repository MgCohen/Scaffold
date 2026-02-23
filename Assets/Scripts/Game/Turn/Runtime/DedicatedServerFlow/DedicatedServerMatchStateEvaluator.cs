using System.Collections.Generic;

namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Provides deterministic state-machine calculations for dedicated server match flow.
    /// </summary>
    public static class DedicatedServerMatchStateEvaluator
    {
        public static MatchFlowStage ResolveStage(IReadOnlyList<PlayerSessionState> players, bool hasStarted)
        {
            var stage = MatchFlowStage.AwaitingConnections;
            var shouldKeepInProgress = hasStarted;
            if (shouldKeepInProgress)
            {
                stage = MatchFlowStage.InProgress;
            }
            else
            {
                var hasForfeitedPlayer = HasForfeitedPlayer(players);
                if (hasForfeitedPlayer)
                {
                    stage = MatchFlowStage.Cancelled;
                }
                else
                {
                    stage = ResolvePreStartStage(players);
                }
            }
            return stage;
        }

        public static MatchFlowStage ResolvePreStartStage(IReadOnlyList<PlayerSessionState> players)
        {
            var stage = MatchFlowStage.AwaitingConnections;
            var allConnected = AreAllPlayersConnected(players);
            var allSynced = AreAllPlayersSnapshotSynced(players);
            var allReady = AreAllPlayersReady(players);
            if (allConnected && !allSynced)
            {
                stage = MatchFlowStage.AwaitingSnapshotSync;
            }
            if (allConnected && allSynced && !allReady)
            {
                stage = MatchFlowStage.AwaitingReady;
            }
            if (allConnected && allSynced && allReady)
            {
                stage = MatchFlowStage.InProgress;
            }
            return stage;
        }

        public static bool AreAllPlayersConnected(IReadOnlyList<PlayerSessionState> players)
        {
            var areAllConnected = players.Count > 0;
            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index];
                var isConnected = player.HasConnection;
                if (!isConnected)
                {
                    areAllConnected = false;
                }
            }
            return areAllConnected;
        }

        public static bool AreAllPlayersSnapshotSynced(IReadOnlyList<PlayerSessionState> players)
        {
            var areAllSynced = players.Count > 0;
            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index];
                var isSynced = player.SnapshotSynced;
                if (!isSynced)
                {
                    areAllSynced = false;
                }
            }
            return areAllSynced;
        }

        public static bool AreAllPlayersReady(IReadOnlyList<PlayerSessionState> players)
        {
            var areAllReady = players.Count > 0;
            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index];
                var isReady = player.Ready;
                if (!isReady)
                {
                    areAllReady = false;
                }
            }
            return areAllReady;
        }

        public static bool HasForfeitedPlayer(IReadOnlyList<PlayerSessionState> players)
        {
            var hasForfeit = false;
            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index];
                var isForfeited = player.Status == PlayerSessionStatus.Forfeited;
                if (isForfeited)
                {
                    hasForfeit = true;
                }
            }
            return hasForfeit;
        }

        public static bool HasDisconnectedPlayer(IReadOnlyList<PlayerSessionState> players)
        {
            var hasDisconnected = false;
            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index];
                var isDisconnected = player.Status == PlayerSessionStatus.Disconnected;
                if (isDisconnected)
                {
                    hasDisconnected = true;
                }
            }
            return hasDisconnected;
        }

        public static bool TryGetPlayerIndexById(IReadOnlyList<PlayerSessionState> players, string playerId, out int playerIndex)
        {
            var foundIndex = -1;
            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index];
                var isTargetPlayer = player.PlayerId == playerId;
                if (isTargetPlayer)
                {
                    foundIndex = index;
                }
            }
            playerIndex = foundIndex;
            var hasPlayer = foundIndex >= 0;
            return hasPlayer;
        }

        public static bool TryGetConnectedPlayerIndexByClientId(IReadOnlyList<PlayerSessionState> players, ulong clientId, out int playerIndex)
        {
            var foundIndex = -1;
            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index];
                var isConnectedPlayer = player.HasConnection && player.ClientId == clientId;
                if (isConnectedPlayer)
                {
                    foundIndex = index;
                }
            }
            playerIndex = foundIndex;
            var hasPlayer = foundIndex >= 0;
            return hasPlayer;
        }

        public static IReadOnlyList<PlayerSessionState> ReplacePlayer(IReadOnlyList<PlayerSessionState> players, int playerIndex, PlayerSessionState updatedPlayer)
        {
            var updatedPlayers = new List<PlayerSessionState>(players);
            var indexIsValid = playerIndex >= 0 && playerIndex < updatedPlayers.Count;
            if (indexIsValid)
            {
                updatedPlayers[playerIndex] = updatedPlayer;
            }
            return updatedPlayers;
        }

        public static List<string> GetExpiredDisconnectedPlayers(IReadOnlyList<PlayerSessionState> players, long nowUtcTicks)
        {
            var expiredPlayers = new List<string>();
            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index];
                var isDisconnected = player.Status == PlayerSessionStatus.Disconnected;
                var isExpired = nowUtcTicks >= player.ReconnectDeadlineUtcTicks;
                if (isDisconnected && isExpired)
                {
                    expiredPlayers.Add(player.PlayerId);
                }
            }
            return expiredPlayers;
        }
    }
}
