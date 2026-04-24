using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;

namespace LiveOps.ModuleFetchData
{
    public interface IReadableDataCache
    {
        Task<T> Get<T>(IExecutionContext context, string key, T defaultValue);

        Task<bool> Exists(IExecutionContext context, string key);

        Task WarmupAsync(IExecutionContext context, IReadOnlyCollection<string>? keys = null);
    }
}
