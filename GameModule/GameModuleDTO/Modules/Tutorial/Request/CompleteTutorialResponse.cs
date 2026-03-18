using GameModuleDTO.Modules.Tutorial;


namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Response model for the tutorial completion request.
    /// </summary>
    public class CompleteTutorialResponse : ModuleResponse
    {
        public CompleteTutorialResponse(TutorialModuleData data)
        {
            Data = data;
        }

        /// <summary>Gets the updated tutorial module data.</summary>
        public TutorialModuleData Data { get; protected set; }

        public override bool IsValid()
        {
            return Data != null;
        }
    }
}
