using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Core.ModuleFetchData
{
    public interface IGameState : IWriteableDataCache, IReadableDataCache
    {
        Task Set(IExecutionContext context, string databaseKey, string key, object value, bool useWriteLock = false);

        Task Delete(IExecutionContext context, string databaseKey, string key);

        /// <summary>
        /// Reads a single item from a Cloud Save custom database namespace (sets active namespace then delegates to keyed read).
        /// </summary>
        Task<T> Get<T>(IExecutionContext context, string databaseKey, string itemKey, T defaultValue);
    }
}
