namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Immutable player session data tracked by the dedicated server flow state machine.
    /// </summary>
    public record PlayerSessionState(string PlayerId, ulong ClientId, bool HasConnection, bool SnapshotSynced, bool Ready, PlayerSessionStatus Status, long LastSeenUtcTicks, long ReconnectDeadlineUtcTicks, int LastAcknowledgedSnapshotVersion);
}
