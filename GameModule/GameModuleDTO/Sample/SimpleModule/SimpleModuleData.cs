using GameModuleDTO.GameModule;

namespace GameModuleDTO.Sample.SimpleModuleData
{
    public class SimpleModuleData : IGameModuleData
    {
        public static string StaticKey { get { return nameof(SimpleModuleData); } }
        public string Key { get { return StaticKey; } }
    }
}