using GameModuleDTO.Modules.Level;
using Utility.Assert;

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
