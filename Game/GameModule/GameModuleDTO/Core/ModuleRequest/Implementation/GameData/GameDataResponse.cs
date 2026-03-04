using System.Linq;
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

        /// <summary>
        /// Confirms the response contains non-null initialized lists internally.
        /// </summary>
        /// <returns>True if the data model holds nested attributes safely.</returns>
        public override bool IsValid()
        {
            return GameData != null && GameData.ModulesData.Any();
        }
    }
}
