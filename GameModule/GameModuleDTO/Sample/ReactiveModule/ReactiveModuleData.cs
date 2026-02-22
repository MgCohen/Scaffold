using GameModuleDTO.GameModule;

namespace GameModuleDTO.Sample.ReactiveModule
{
    public class ReactiveModuleData : IGameModuleData
    {
        public static string StaticKey { get { return nameof(ReactiveModuleData); } }
        public string Key { get { return StaticKey; } }
    }
}