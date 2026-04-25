using System.Threading.Tasks;
using LiveOps.DTO.Keys;
using Unity.Services.CloudCode.Core;

namespace LiveOps.ModuleFetchData
{
    public static class DataCacheExtensions
    {
        public static Task Set<T>(this IWriteableDataCache cache, IExecutionContext context, T value, bool useWriteLock = false)
        {
            return cache.Set(context, KeyOf<T>.Module, value, useWriteLock);
        }

        public static Task<T> Get<T>(this IReadableDataCache cache, IExecutionContext context, T defaultValue)
        {
            return cache.Get(context, KeyOf<T>.Module, defaultValue);
        }

        public static async Task<T> GetOrSet<TCache, T>(this TCache cache, IExecutionContext context, T defaultValue, bool useWriteLock = false)
            where TCache : IReadableDataCache, IWriteableDataCache
        {
            string key = KeyOf<T>.Module;
            if (await cache.Exists(context, key).ConfigureAwait(false))
            {
                return await cache.Get(context, key, defaultValue).ConfigureAwait(false);
            }

            await cache.Set(context, key, defaultValue, useWriteLock).ConfigureAwait(false);
            return defaultValue;
        }
    }
}
