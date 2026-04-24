using System;

namespace LiveOps.Initialize
{

    public readonly struct LiveOpsManifestEntry
    {
        public LiveOpsManifestEntry(System.Type type, bool isGameApiHandler, bool isGameModule)
        {
            Type = type;
            IsGameApiHandler = isGameApiHandler;
            IsGameModule = isGameModule;
        }

        public System.Type Type { get; }

        public bool IsGameApiHandler { get; }

        public bool IsGameModule { get; }
    }
}
