using Newtonsoft.Json;

namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Request to complete a specific tutorial step.
    /// </summary>
    public class CompleteTutorialRequest : ModuleRequest<CompleteTutorialResponse>
    {
        public CompleteTutorialRequest(int tutorialId)
        {
            TutorialId = tutorialId;
        }

        [JsonProperty]
        public int TutorialId { get; set; }

        public override void AssertModule()
        {
        }
    }
}
