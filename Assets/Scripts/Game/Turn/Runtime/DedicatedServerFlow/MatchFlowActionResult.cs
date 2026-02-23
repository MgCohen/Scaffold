namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Generic operation result for dedicated server flow commands.
    /// </summary>
    public record MatchFlowActionResult(MatchFlowActionCode Code, bool RequiresSnapshotSync, bool MatchStarted, bool WaitingForReconnect);
}
