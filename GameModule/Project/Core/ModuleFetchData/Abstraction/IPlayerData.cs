using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;

namespace GameModule.ModuleFetchData
{
    public interface IPlayerData : IWriteableDataCache, IReadableDataCache
    {
        string PlayerId { get; }
        Task Delete(IExecutionContext context, string key);
        string GetWriteLock(string key);
    }
}
