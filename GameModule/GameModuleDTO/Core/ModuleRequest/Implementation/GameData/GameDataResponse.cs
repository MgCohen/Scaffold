using GameModuleDTO.GameModule;

namespace GameModuleDTO.ModuleRequests
{
    public class GameDataResponse : ModuleResponse
    {
        public GameData GameData { get; protected set; }

        public GameDataResponse(GameData gameData)
        {
            this.GameData = gameData;
        }

        public override bool IsValid()
        {
            return GameData != null && GameData.modulesData.Any();
        }
    }
}