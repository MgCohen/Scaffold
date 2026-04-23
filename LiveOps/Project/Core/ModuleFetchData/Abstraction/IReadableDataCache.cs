using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;

namespace GameModule.ModuleFetchData
{
    public interface IReadableDataCache
    {
        Task<T> Get<T>(IExecutionContext context, string key, T defaultValue);

        Task<bool> Exists(IExecutionContext context, string key);

        /// <summary>
        /// Prefetches data into the cache. <c>null</c> or non-empty keys: full snapshot (until selective fetch exists).
        /// Empty collection: skip prefetch (lazy on first <see cref="Get{T}"/>).
        /// </summary>
        Task WarmupAsync(IExecutionContext context, IReadOnlyCollection<string>? keys = null);
    }
}
