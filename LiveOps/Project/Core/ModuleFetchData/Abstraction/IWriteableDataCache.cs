using System;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;

namespace GameModule.ModuleFetchData
{
    public interface IWriteableDataCache
    {
        /// <summary>
        /// Inside an active batch from <see cref="BeginBatch"/>, updates cache only; otherwise write-through to backing store.
        /// </summary>
        Task Set(IExecutionContext context, string key, object value, bool useWriteLock = false);

        /// <summary>
        /// Persists all dirty entries (never a single-key partial flush).
        /// </summary>
        Task FlushAsync(IExecutionContext context);

        Task Delete(IExecutionContext context, string key);

        /// <summary>
        /// Opens a batch scope. Disposing the outermost scope calls <see cref="FlushAsync"/>.
        /// </summary>
        IAsyncDisposable BeginBatch();
    }
}
