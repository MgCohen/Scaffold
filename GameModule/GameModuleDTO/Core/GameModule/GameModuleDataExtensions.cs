using GameModuleDTO.GameModule;

public static class GameDataExtensions
{
    public static string GetKey<T>() where T : IGameModuleData
    {
        return nameof(T);
    }
}