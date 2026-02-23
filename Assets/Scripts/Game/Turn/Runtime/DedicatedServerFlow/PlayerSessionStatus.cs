namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Represents a single player's authoritative session status inside a match.
    /// </summary>
    public enum PlayerSessionStatus
    {
        Assigned,
        Connected,
        SnapshotSynced,
        Ready,
        Disconnected,
        Forfeited
    }
}
