using GameModuleDTO.GameModule;
using GameModuleDTO.Modules.Common;

namespace GameModuleDTO.Modules.Tutorial
{
    /// <summary>
    /// Data model for the Tutorial module.
    /// </summary>
    public class TutorialModuleData : MultiProgressModuleData
    {
        /// <summary>Gets the resolved classification name for the component.</summary>
        public override string Key { get { return GameDataExtensions.GetKey<TutorialModuleData>(); } }
    }
}
