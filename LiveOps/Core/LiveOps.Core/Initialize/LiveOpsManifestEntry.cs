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
            RequestType = null;
            ResponseType = null;
        }

        public LiveOpsManifestEntry(
            System.Type type,
            bool isGameApiHandler,
            bool isGameModule,
            System.Type? requestType,
            System.Type? responseType)
        {
            Type = type;
            IsGameApiHandler = isGameApiHandler;
            IsGameModule = isGameModule;
            RequestType = requestType;
            ResponseType = responseType;
        }

        public System.Type Type { get; }

        public bool IsGameApiHandler { get; }

        public bool IsGameModule { get; }

        public System.Type? RequestType { get; }

        public System.Type? ResponseType { get; }
    }
}
