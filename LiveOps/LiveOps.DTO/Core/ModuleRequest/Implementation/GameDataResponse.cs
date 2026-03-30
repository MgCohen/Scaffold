using GameModuleDTO.GameModule;

namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Serves as the standard response delivering the gathered game module payload block.
    /// </summary>
    public class GameDataResponse : ModuleResponse
    {
        /// <summary>Gets the core game data configuration mapped over the network.</summary>
        public GameData GameData { get; protected set; }

        /// <summary>
        /// Initializes a newly received data block efficiently safely.
        /// </summary>
        /// <param name="gameData">The expected data implementation format.</param>
        public GameDataResponse(GameData gameData)
        {
            this.GameData = gameData;
        }
    }
}