using System.Threading;
using System.Threading.Tasks;
using LiveOps.DTO.GameModule;
using LiveOps.GameApi;

namespace LiveOps.GameModule
{

    public interface IGameModule
    {

        public string Key { get; }

        Task<IGameModuleData> InitializeAsync(
            GameApiSession session,
            CancellationToken cancellationToken = default);

        string[]? PlayerKeys() => null;

        string[]? ConfigKeys() => null;
    }
}
