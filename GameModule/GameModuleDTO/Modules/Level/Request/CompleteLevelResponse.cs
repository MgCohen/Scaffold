using GameModuleDTO.Modules.Level;


namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Response model for the level completion request.
    /// </summary>
    public class CompleteLevelResponse : ModuleResponse
    {
        public CompleteLevelResponse(LevelModuleData data)
        {
            Data = data;
        }

        /// <summary>Gets the updated level module data.</summary>
        public LevelModuleData Data { get; protected set; }

        public override bool IsValid()
        {
            return Data != null;
        }
    }
}
