using LiveOps.Core.DTO.ModuleRequest;
using GameDataPayload = LiveOps.Core.DTO.GameModule.GameData;

namespace LiveOps.Modules.DTO.GameData
{
    /// <summary>
    /// Serves as the standard response delivering the gathered game module payload block.
    /// </summary>
    public class GameDataResponse : ModuleResponse
    {
        /// <summary>Gets the core game data configuration mapped over the network.</summary>
        public GameDataPayload GameData { get; protected set; }

        /// <summary>
        /// Initializes a newly received data block efficiently safely.
        /// </summary>
        /// <param name="gameData">The expected data implementation format.</param>
        public GameDataResponse(GameDataPayload gameData)
        {
            GameData = gameData;
        }
    }
}