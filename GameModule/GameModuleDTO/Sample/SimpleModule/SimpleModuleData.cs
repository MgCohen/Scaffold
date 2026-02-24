using GameModuleDTO.GameModule;

namespace GameModuleDTO.Sample.SimpleModule
{
    public class SimpleModuleData : IGameModuleData
    {
        public string Key { get { return GameDataExtensions.GetKey<SimpleModuleData>(); } }
    }
}