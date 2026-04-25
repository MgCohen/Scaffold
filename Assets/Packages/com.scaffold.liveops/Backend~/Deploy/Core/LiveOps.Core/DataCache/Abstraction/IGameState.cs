using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;

namespace LiveOps.ModuleFetchData
{
    public interface IGameState : IWriteableDataCache, IReadableDataCache
    {
        Task Set(IExecutionContext context, string databaseKey, string key, object value, bool useWriteLock = false);

        Task Delete(IExecutionContext context, string databaseKey, string key);

        Task<T> Get<T>(IExecutionContext context, string databaseKey, string itemKey, T defaultValue);
    }
}
