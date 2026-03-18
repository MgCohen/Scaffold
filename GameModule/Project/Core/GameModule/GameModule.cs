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
        /// <summary>Gets a value indicating whether this module serves the client.</summary>
        public abstract bool Client { get; }

        /// <summary>Gets a value indicating whether this module serves the server.</summary>
        public abstract bool Server { get; }

        /// <summary>Gets the component key mapped dynamically.</summary>
        public string Key
        {
            get
            {
                return GameDataExtensions.GetKey<T>();
            }
        }

        /// <summary>
        /// Initiates the module and delegates logic handling.
        /// </summary>
        /// <param name="context">The execution context.</param>
        /// <param name="playerData">The player active session logic.</param>
        /// <param name="gameState">The game state logic wrapper.</param>
        /// <param name="remoteConfig">The remote configuration dependencies.</param>
        /// <returns>A mapped execution promise payload.</returns>
        public abstract Task<IGameModuleData> Initialize(IExecutionContext context, PlayerData playerData, GameState gameState, RemoteConfig remoteConfig);
    }
}