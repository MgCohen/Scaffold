using System.Threading;
using System.Threading.Tasks;
using LiveOps.DTO.GameModule;
using LiveOps.GameApi;

namespace LiveOps.GameModule
{

    public abstract class GameModule<T> : IGameModule where T : IGameModuleData
    {

        public string Key => typeof(T).Name;

        public abstract Task<IGameModuleData> InitializeAsync(
            GameApiSession session,
            CancellationToken cancellationToken = default);
    }
}
