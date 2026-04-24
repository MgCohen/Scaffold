using LiveOps.DTO.GameApi;
using LiveOps.DTO.ModuleRequest;

namespace LiveOps.Modules.DTO.GameData
{

    [UsesGameApi]
    [GameApiKey("GameData")]
    public class GameDataRequest : ModuleRequest<GameDataResponse>
    {

        public GameDataRequest()
        {
        }
    }
}
