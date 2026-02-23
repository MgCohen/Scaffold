using System.Collections.Generic;
using Scaffold.States;
using Scaffold.Turns.DedicatedServerFlow;

namespace Scaffold.DedicatedServerFlow.Samples
{
    /// <summary>
    /// Sample usage of dedicated server match flow: build store and service, initialize match, connect players, sync snapshot, and mark ready.
    /// </summary>
    public static class DedicatedServerFlowSample
    {
        private const string SampleMatchId = "sample-match-1";
        private const int SampleProtocolVersion = 1;
        private const long ReconnectGraceTicks = 60L * 10_000_000; // 60 seconds in ticks

        public static IDedicatedServerMatchFlowService RunBasicFlow()
        {
            var store = DedicatedServerMatchFlowBuilder.BuildStore();
            var service = DedicatedServerMatchFlowBuilder.BuildService(store);
            var playerIds = new List<string> { "player-a", "player-b" };
            service.InitializeMatch(SampleMatchId, SampleProtocolVersion, 0, 0, ReconnectGraceTicks, playerIds);
            return service;
        }

        public static DedicatedServerMatchCoordinator RunWithCoordinator()
        {
            var store = DedicatedServerMatchFlowBuilder.BuildStore();
            var service = DedicatedServerMatchFlowBuilder.BuildService(store);
            var playerIds = new List<string> { "player-a", "player-b" };
            service.InitializeMatch(SampleMatchId, SampleProtocolVersion, 0, 0, ReconnectGraceTicks, playerIds);
            var coordinator = new DedicatedServerMatchCoordinator(service, SampleMatchId, SampleProtocolVersion);
            return coordinator;
        }

        public static MatchFlowActionResult ConnectPlayer(DedicatedServerMatchCoordinator coordinator, string playerId, ulong clientId, long nowUtcTicks)
        {
            var result = coordinator.OnPlayerConnected(playerId, clientId, nowUtcTicks);
            return result;
        }

        public static MatchFlowActionResult AckSnapshot(DedicatedServerMatchCoordinator coordinator, string playerId, int snapshotVersion, int snapshotHash, long nowUtcTicks)
        {
            var result = coordinator.OnSnapshotAcknowledged(playerId, snapshotVersion, snapshotHash, nowUtcTicks);
            return result;
        }

        public static MatchFlowActionResult MarkPlayerReady(DedicatedServerMatchCoordinator coordinator, string playerId, long nowUtcTicks)
        {
            var result = coordinator.OnPlayerReady(playerId, nowUtcTicks);
            return result;
        }
    }
}
