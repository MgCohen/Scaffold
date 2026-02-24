using GameModuleDTO.GameModule;

public static class GameDataExtensions
{
    public static string GetKey<T>(this T gameData) where T : IGameModuleData
    {
        return nameof(T);
    }

    public static string GetKey<T>() where T : IGameModuleData
    {
        return nameof(T);
    }
}