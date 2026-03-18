using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;
using GameModuleDTO.GameModule;

namespace GameModule.ModuleFetchData
{
    public interface IReadableDataCache
    {
        Task<T> Get<T>(IExecutionContext context, string key, T defaultValue);
        Task<T> Get<T>(IExecutionContext context, T defaultValue) where T : IGameModuleData;
        Task<Dictionary<string, T>> GetAllValues<T>(IExecutionContext context);
        Task<bool> Exists(IExecutionContext context, string key);
        Task<string> GetRaw(IExecutionContext context, string key);
    }
}
