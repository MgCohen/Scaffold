using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;

using GameModuleDTO.GameModule;
using Unity.Services.CloudCode.Core;

namespace GameModule.GameModule
{
    /// <summary>
    /// Strongly-typed abstract base for game modules mapping a specific data type.
    /// </summary>
    /// <typeparam name="T">The module data type constrained to IGameModuleData implementations.</typeparam>
    public abstract class GameModule<T> : IGameModule where T : IGameModuleData
    {
        /// <summary>Gets the component key mapped dynamically.</summary>
        public string Key => typeof(T).Name;

        /// <summary>
        /// Initiates the module and delegates logic handling.
        /// </summary>
        /// <param name="context">The execution context.</param>
        /// <param name="Player">The player active session logic.</param>
        /// <param name="gameState">The game state logic wrapper.</param>
        /// <param name="remoteConfig">The remote configuration dependencies.</param>
        /// <returns>A mapped execution promise payload.</returns>
        public abstract Task<IGameModuleData> Initialize(IExecutionContext context, IPlayerData Player, IGameState gameState, IRemoteConfig remoteConfig);
    }
}