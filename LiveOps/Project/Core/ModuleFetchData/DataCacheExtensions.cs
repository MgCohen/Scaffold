using System.Threading.Tasks;
using GameModuleDTO.GameModule;
using Unity.Services.CloudCode.Core;

namespace GameModule.ModuleFetchData
{
    /// <summary>
    /// Helpers that depend on <see cref="IGameModuleData"/>; kept off cache interfaces by design.
    /// </summary>
    public static class DataCacheExtensions
    {
        public static Task Set(this IWriteableDataCache cache, IExecutionContext context, IGameModuleData value, bool useWriteLock = false)
        {
            return cache.Set(context, value.Key, value, useWriteLock);
        }

        public static Task<T> Get<T>(this IReadableDataCache cache, IExecutionContext context, T defaultValue)
            where T : IGameModuleData
        {
            return cache.Get(context, typeof(T).Name, defaultValue);
        }

        public static async Task<T> GetOrSet<TCache, T>(this TCache cache, IExecutionContext context, T defaultValue, bool useWriteLock = false)
            where TCache : IReadableDataCache, IWriteableDataCache
            where T : IGameModuleData
        {
            string key = typeof(T).Name;
            if (await cache.Exists(context, key).ConfigureAwait(false))
            {
                return await cache.Get(context, key, defaultValue).ConfigureAwait(false);
            }

            await cache.Set(context, key, defaultValue, useWriteLock).ConfigureAwait(false);
            return defaultValue;
        }
    }
}
