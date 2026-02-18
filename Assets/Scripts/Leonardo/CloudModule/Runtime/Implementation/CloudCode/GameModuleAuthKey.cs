namespace Scaffold.CloudModules.Shared
{
    //TODO: Refactor
    public class GameModuleAuthKey
    {
#if SERVER
        public static string guid = "lalala";
#else
        public static string guid;
#endif
#if SERVER || UNITY_EDITOR
        public static string guidue = "lalala";
#else
        public static string guidue;
#endif
    }
}