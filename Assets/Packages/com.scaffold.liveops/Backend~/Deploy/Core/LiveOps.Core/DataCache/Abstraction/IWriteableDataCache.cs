using System;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;

namespace LiveOps.ModuleFetchData
{
    public interface IWriteableDataCache
    {

        Task Set(IExecutionContext context, string key, object value, bool useWriteLock = false);

        Task FlushAsync(IExecutionContext context);

        Task Delete(IExecutionContext context, string key);

        IAsyncDisposable BeginBatch();
    }
}
