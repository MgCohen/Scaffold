namespace GameModule.ModuleFetchData
{
    public interface IPlayerData : IWriteableDataCache, IReadableDataCache
    {
        string PlayerId { get; }
    }
}
