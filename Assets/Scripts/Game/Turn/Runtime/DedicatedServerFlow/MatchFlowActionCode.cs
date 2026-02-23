namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Normalized result codes for dedicated server flow operations.
    /// </summary>
    public enum MatchFlowActionCode
    {
        Accepted,
        MatchNotInitialized,
        MatchMismatch,
        ProtocolMismatch,
        UnknownPlayer,
        AlreadyConnected,
        PlayerForfeited,
        PlayerNotConnected,
        SnapshotVersionMismatch,
        SnapshotHashMismatch,
        SnapshotNotSynced,
        UnknownClient,
        PlayerNotDisconnected,
        ReconnectWindowExpired,
        NoExpiredConnections
    }
}
