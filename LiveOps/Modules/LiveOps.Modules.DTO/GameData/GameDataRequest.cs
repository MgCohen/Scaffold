using LiveOps.DTO.Keys;
using LiveOps.DTO.ModuleRequest;

namespace LiveOps.Modules.DTO.GameData
{
    [LiveOpsKey("GameDataRequest")]
    public class GameDataRequest : ModuleRequest<GameDataResponse>
    {
        public GameDataRequest()
        {
        }
    }
}
