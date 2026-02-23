namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Represents the authoritative lifecycle of a server-side match after matchmaking.
    /// </summary>
    public enum MatchFlowStage
    {
        Idle,
        AwaitingConnections,
        AwaitingSnapshotSync,
        AwaitingReady,
        InProgress,
        Completed,
        Cancelled
    }
}
