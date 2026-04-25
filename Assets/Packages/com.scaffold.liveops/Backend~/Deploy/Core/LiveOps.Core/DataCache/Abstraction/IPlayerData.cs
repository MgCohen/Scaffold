namespace LiveOps.ModuleFetchData
{
    public interface IPlayerData : IWriteableDataCache, IReadableDataCache
    {
        string PlayerId { get; }
    }
}
