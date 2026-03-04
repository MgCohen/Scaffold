using GameModuleDTO.GameModule;

namespace GameModuleDTO.Sample.SimpleModule
{
    /// <summary>
    /// Represents an empty sample payload for simple structural tests natively.
    /// </summary>
    public class SimpleModuleData : IGameModuleData
    {
        /// <summary>Gets the generic identifier statically binding accurately.</summary>
        public string Key { get { return GameDataExtensions.GetKey<SimpleModuleData>(); } }
    }
}
