using System.Collections.Generic;
using System.Threading.Tasks;
using GameModuleDTO.GameModule;
using Unity.Services.CloudCode.Core;

namespace GameModule.ModuleFetchData
{
    public interface IWriteableDataCache
    {
        Task Set(IExecutionContext context, string key, object value, bool useWriteLock = false);
        Task Set(IExecutionContext context, IGameModuleData value, bool useWriteLock = false);
        Task SetBatch(IExecutionContext context, IEnumerable<IGameModuleData> values, bool useWriteLock = false);
        Task Delete(IExecutionContext context, string key);
        Task SaveCache(IExecutionContext context);
        void AddToCache(params string[] moduleKeys);
        void AddToCache(IGameModuleData moduleData);
        Task<T> GetOrSet<T>(IExecutionContext context, T defaultValue, bool useWriteLock = false) where T : IGameModuleData;
    }
}
