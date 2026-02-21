using GameModuleDTO.GameModule;

namespace GameModuleDTO.Sample.ReactiveModule
{
    public class ReactiveModuleData : IGameModuleData
    {
        public string Key { get { return nameof(ReactiveModuleData); } }
        public static string StaticKey { get { return nameof(ReactiveModuleData); } }
    }
}