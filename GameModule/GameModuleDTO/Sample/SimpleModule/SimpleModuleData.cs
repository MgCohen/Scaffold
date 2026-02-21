using GameModuleDTO.GameModule;

namespace GameModuleDTO.Sample.SimpleModuleData
{
    public class SimpleModuleData : IGameModuleData
    {
        public string Key { get { return nameof(SimpleModuleData); } }
        public static string StaticKey { get { return nameof(SimpleModuleData); } }
    }
}