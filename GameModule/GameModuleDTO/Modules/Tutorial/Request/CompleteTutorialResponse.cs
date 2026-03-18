using GameModuleDTO.Modules.Tutorial;
using Utility.Assert;

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

        /// <summary>
        /// Validates that the returned data is not null.
        /// </summary>
        /// <returns>True if valid.</returns>
        public override bool IsValid()
        {
            return Assert.IsTrue(Data != null, $"{nameof(Data)} must not be null");
        }
    }
}
