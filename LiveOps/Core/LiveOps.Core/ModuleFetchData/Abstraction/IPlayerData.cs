namespace LiveOps.Core.ModuleFetchData
{
    public interface IPlayerData : IWriteableDataCache, IReadableDataCache
    {
        string PlayerId { get; }
    }
}
