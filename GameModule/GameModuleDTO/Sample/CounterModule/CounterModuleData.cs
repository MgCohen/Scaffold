using GameModuleDTO.GameModule;

namespace GameModuleDTO.Sample.CounterModule
{
    public class CounterModuleData : IGameModuleData
    {
        public string Key { get { return nameof(CounterModuleData); } }
        public static string StaticKey { get { return nameof(CounterModuleData); } }
    }
}