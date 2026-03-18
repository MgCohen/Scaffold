using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;

namespace GameModule.ModuleFetchData
{
    public interface IReadableDataCache
    {
        Task<T> Get<T>(IExecutionContext context, string key, T defaultValue);
        Task<Dictionary<string, T>> GetAllValues<T>(IExecutionContext context);
        Task<bool> Exists(IExecutionContext context, string key);
        Task<string> GetRaw(IExecutionContext context, string key);
    }
}
