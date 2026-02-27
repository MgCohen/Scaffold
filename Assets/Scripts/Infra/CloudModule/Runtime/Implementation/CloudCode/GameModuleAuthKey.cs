namespace Scaffold.CloudModules
{
    /// <summary>
    /// Stores the authentication keys required to bypass or validate module interactions.
    /// The main goal is to hold static references to access identifiers used by backend services.
    /// It is used during the network handshake or API queries to assert the client's authority.
    /// </summary>
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