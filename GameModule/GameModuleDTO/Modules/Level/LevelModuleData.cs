using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Common;

namespace GameModuleDTO.Modules.Level
{
    /// <summary>
    /// Data model for the Level module.
    /// </summary>
    public class LevelModuleData : MultiProgressModuleData
    {
        /// <summary>Gets the resolved classification name for the component.</summary>
        public override string Key { get { return GameDataExtensions.GetKey<LevelModuleData>(); } }
    }
}
