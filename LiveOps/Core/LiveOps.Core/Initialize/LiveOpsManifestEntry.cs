using System;

namespace LiveOps.Initialize
{
    /// <summary>
    /// Compile-time entry describing a type discovered for Cloud Code GameApi and/or module registration.
    /// Filled by the <c>Scaffold.LiveOps.Bootstrap.Generators</c> source generator (see <see cref="LiveOpsManifest" /> in the deploy project).
    /// </summary>
    public readonly struct LiveOpsManifestEntry
    {
        public LiveOpsManifestEntry(System.Type type, bool isGameApiHandler, bool isGameModule)
        {
            Type = type;
            IsGameApiHandler = isGameApiHandler;
            IsGameModule = isGameModule;
        }

        /// <summary>Concrete service type to register in DI.</summary>
        public System.Type Type { get; }

        public bool IsGameApiHandler { get; }

        public bool IsGameModule { get; }
    }
}
