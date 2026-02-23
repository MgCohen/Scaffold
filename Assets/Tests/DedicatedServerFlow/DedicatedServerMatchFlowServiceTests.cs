using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.States;
using Scaffold.Turns.DedicatedServerFlow;

namespace Scaffold.Turns.DedicatedServerFlow.Tests
{
    /// <summary>
    /// Edit-mode tests for dedicated server match flow service.
    /// </summary>
    public class DedicatedServerMatchFlowServiceTests
    {
        private const string MatchId = "test-match-1";
        private const int ProtocolVersion = 1;
        private const int SnapshotVersion = 0;
        private const int SnapshotHash = 0;
        private const long ReconnectGraceTicks = 60L * 10_000_000;
        private const ulong ClientIdA = 1UL;
        private const ulong ClientIdB = 2UL;

        private Store store;
        private IDedicatedServerMatchFlowService service;
        private long nowUtcTicks;

        [SetUp]
        public void SetUp()
        {
            store = DedicatedServerMatchFlowBuilder.BuildStore();
            service = DedicatedServerMatchFlowBuilder.BuildService(store);
            nowUtcTicks = 1000L;
            var playerIds = new List<string> { "player-a", "player-b" };
            service.InitializeMatch(MatchId, ProtocolVersion, SnapshotVersion, SnapshotHash, ReconnectGraceTicks, playerIds);
        }

        [Test]
        public void GetState_AfterInitialize_ReturnsAwaitingConnections()
        {
            var state = service.GetState();
            Assert.That(state.Stage, Is.EqualTo(MatchFlowStage.AwaitingConnections));
            Assert.That(state.MatchId, Is.EqualTo(MatchId));
            Assert.That(state.Players.Count, Is.EqualTo(2));
        }

        [Test]
        public void TryConnect_ValidPlayer_ReturnsAccepted()
        {
            var result = service.TryConnect(MatchId, "player-a", ClientIdA, ProtocolVersion, nowUtcTicks);
            Assert.That(result.Code, Is.EqualTo(MatchFlowActionCode.Accepted));
            Assert.That(result.RequiresSnapshotSync, Is.True);
        }

        [Test]
        public void TryConnect_WrongMatchId_ReturnsMatchMismatch()
        {
            var result = service.TryConnect("wrong-match", "player-a", ClientIdA, ProtocolVersion, nowUtcTicks);
            Assert.That(result.Code, Is.EqualTo(MatchFlowActionCode.MatchMismatch));
        }

        [Test]
        public void TryConnect_UnknownPlayer_ReturnsUnknownPlayer()
        {
            var result = service.TryConnect(MatchId, "unknown-player", ClientIdA, ProtocolVersion, nowUtcTicks);
            Assert.That(result.Code, Is.EqualTo(MatchFlowActionCode.UnknownPlayer));
        }

        [Test]
        public void TryConnect_ThenAcknowledgeSnapshot_ThenMarkReady_TransitionsCorrectly()
        {
            service.TryConnect(MatchId, "player-a", ClientIdA, ProtocolVersion, nowUtcTicks);
            service.TryConnect(MatchId, "player-b", ClientIdB, ProtocolVersion, nowUtcTicks);
            service.UpdateSnapshotCheckpoint(1, 100);
            var ackA = service.AcknowledgeSnapshot("player-a", 1, 100, nowUtcTicks);
            var ackB = service.AcknowledgeSnapshot("player-b", 1, 100, nowUtcTicks);
            Assert.That(ackA.Code, Is.EqualTo(MatchFlowActionCode.Accepted));
            Assert.That(ackB.Code, Is.EqualTo(MatchFlowActionCode.Accepted));
            var readyA = service.MarkReady("player-a", nowUtcTicks);
            var readyB = service.MarkReady("player-b", nowUtcTicks);
            Assert.That(readyA.Code, Is.EqualTo(MatchFlowActionCode.Accepted));
            Assert.That(readyB.Code, Is.EqualTo(MatchFlowActionCode.Accepted));
            var state = service.GetState();
            Assert.That(state.Stage, Is.EqualTo(MatchFlowStage.InProgress));
            Assert.That(state.HasStarted, Is.True);
        }

        [Test]
        public void AcknowledgeSnapshot_WithoutConnect_ReturnsPlayerNotConnected()
        {
            var result = service.AcknowledgeSnapshot("player-a", SnapshotVersion, SnapshotHash, nowUtcTicks);
            Assert.That(result.Code, Is.EqualTo(MatchFlowActionCode.PlayerNotConnected));
        }

        [Test]
        public void MarkReady_WithoutSnapshotSync_ReturnsSnapshotNotSynced()
        {
            service.TryConnect(MatchId, "player-a", ClientIdA, ProtocolVersion, nowUtcTicks);
            var result = service.MarkReady("player-a", nowUtcTicks);
            Assert.That(result.Code, Is.EqualTo(MatchFlowActionCode.SnapshotNotSynced));
        }

        [Test]
        public void HandleDisconnect_UnknownClient_ReturnsUnknownClient()
        {
            var result = service.HandleDisconnect(999UL, nowUtcTicks);
            Assert.That(result.Code, Is.EqualTo(MatchFlowActionCode.UnknownClient));
        }

        [Test]
        public void HandleDisconnect_ConnectedPlayer_ReturnsAccepted()
        {
            service.TryConnect(MatchId, "player-a", ClientIdA, ProtocolVersion, nowUtcTicks);
            var result = service.HandleDisconnect(ClientIdA, nowUtcTicks);
            Assert.That(result.Code, Is.EqualTo(MatchFlowActionCode.Accepted));
            Assert.That(result.WaitingForReconnect, Is.True);
        }
    }
}
