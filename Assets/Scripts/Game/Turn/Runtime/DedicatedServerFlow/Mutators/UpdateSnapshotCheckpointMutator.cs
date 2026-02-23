using Scaffold.States;

namespace Scaffold.Turns.DedicatedServerFlow
{
    /// <summary>
    /// Updates the authoritative snapshot descriptor used for sync and reconnect validation.
    /// </summary>
    public class UpdateSnapshotCheckpointMutator : Mutator<DedicatedServerMatchState>
    {
        private readonly int snapshotVersion;
        private readonly int snapshotHash;

        public UpdateSnapshotCheckpointMutator(int snapshotVersion, int snapshotHash)
        {
            this.snapshotVersion = snapshotVersion;
            this.snapshotHash = snapshotHash;
        }

        public override DedicatedServerMatchState Change(DedicatedServerMatchState state)
        {
            var updatedState = state with { SnapshotVersion = snapshotVersion, SnapshotHash = snapshotHash };
            return updatedState;
        }
    }
}
