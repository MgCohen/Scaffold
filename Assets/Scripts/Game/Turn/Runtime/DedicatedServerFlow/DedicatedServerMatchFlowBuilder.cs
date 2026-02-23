using System;
using Scaffold.States;

namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Builder helpers for creating a Store slice and service instance for dedicated server match flow.
    /// </summary>
    public static class DedicatedServerMatchFlowBuilder
    {
        public static Store BuildStore()
        {
            var players = Array.Empty<PlayerSessionState>();
            var initialState = new DedicatedServerMatchState(string.Empty, 0, MatchFlowStage.Idle, 0, 0, 0, false, players);
            var builder = new StoreBuilder();
            var configuredBuilder = builder.BuildSlice(initialState);
            var store = configuredBuilder.Build();
            return store;
        }

        public static IDedicatedServerMatchFlowService BuildService(Store store)
        {
            var service = new DedicatedServerMatchFlowService(store);
            return service;
        }
    }
}
