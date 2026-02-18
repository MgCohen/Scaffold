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
        
        protected T FirstModuleAsT<T>() where T : IGameModuleData
        {
            return (T)GameData?.modulesData.FirstOrDefault(x => x.GetType() == typeof(T));
        }
    }
}