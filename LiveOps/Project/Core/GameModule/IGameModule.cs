using GameModuleDTO.GameModule;
using GameModule.ModuleFetchData;

using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;

namespace GameModule.GameModule
{
    /// <summary>
    /// Provides the base contract for initializing independent game module instances.
    /// </summary>
    public interface IGameModule
    {
        /// <summary>Gets the unique authorization mapping identification.</summary>
        public string Key { get; }

        /// <summary>
        /// Initializes the module by fetching the relevant data from the game state.
        /// </summary>
        /// <param name="context">The execution context.</param>
        /// <param name="Player">The active player data.</param>
        /// <param name="gameState">The current game state.</param>
        /// <param name="remoteConfig">The remote configuration settings.</param>
        /// <returns>The populated module data.</returns>
        public Task<IGameModuleData> Initialize(IExecutionContext context, IPlayerData Player, IGameState gameState, IRemoteConfig remoteConfig);
    }
}