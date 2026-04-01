namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Represents a network request for aggregated game module data.
    /// The server builds <see cref="GameDataResponse"/> from every game module registered in cloud DI (see <c>ModuleConfig</c>).
    /// </summary>
    public class GameDataRequest : ModuleRequest<GameDataResponse>
    {
        /// <summary>Initializes a new instance for deserialization.</summary>
        public GameDataRequest()
        {
        }
    }
}
