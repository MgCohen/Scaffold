namespace GameModule.ModuleFetchData
{
    /// <summary>
    /// Contract for fetching remote configuration settings.
    /// cleanly segregating read-only operations from writable caches.
    /// </summary>
    public interface IRemoteConfig : IReadableDataCache
    {
    }
}
