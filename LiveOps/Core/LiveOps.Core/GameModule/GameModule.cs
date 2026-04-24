using System.Threading;
using System.Threading.Tasks;
using LiveOps.DTO.GameModule;
using LiveOps.DTO.Keys;
using LiveOps.GameApi;

namespace LiveOps.GameModule
{
    public abstract class GameModule<T> : IGameModule where T : IGameModuleData
    {
        public string Key => KeyOf<T>.Module;

        public abstract Task<IGameModuleData> InitializeAsync(
            GameApiSession session,
            CancellationToken cancellationToken = default);
    }
}
