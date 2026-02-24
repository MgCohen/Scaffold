//TODO: Not sure if this is the best idea but works for now

using GameModuleDTO.GameModule;

public abstract class ModuleDataToSave
{
    public List<string> ModulesRequired { get; protected set; } = new List<string>();
}

public class ModuleDataToSave<T1> : ModuleDataToSave where T1 : IGameModuleData
{
    public ModuleDataToSave()
    {
        ModulesRequired.Add(GameDataExtensions.GetKey<T1>());
    }
}

public class ModuleDataToSave<T1, T2> : ModuleDataToSave where T1 : IGameModuleData where T2 : IGameModuleData
{
    public ModuleDataToSave()
    {
        ModulesRequired.AddRange([GameDataExtensions.GetKey<T1>(), GameDataExtensions.GetKey<T2>()]);
    }
}

public class ModuleDataToSave<T1, T2, T3> : ModuleDataToSave where T1 : IGameModuleData where T2 : IGameModuleData where T3 : IGameModuleData
{
    public ModuleDataToSave()
    {
        ModulesRequired.AddRange([GameDataExtensions.GetKey<T1>(), GameDataExtensions.GetKey<T2>(), GameDataExtensions.GetKey<T3>()]);
    }
}
