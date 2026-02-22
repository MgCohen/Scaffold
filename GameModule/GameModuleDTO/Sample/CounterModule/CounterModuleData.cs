using GameModuleDTO.GameModule;

namespace GameModuleDTO.Sample.CounterModule
{
    public class CounterModuleData : IGameModuleData
    {
        public static string StaticKey { get { return nameof(CounterModuleData); } }
        public string Key { get { return StaticKey; } }
    }
}