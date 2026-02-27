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
        /// <summary>
        /// The universally unique identifier string for server builds.
        /// The main goal is to securely verify server side interactions.
        /// It is used automatically in RPC headers.
        /// </summary>
        public static string guid = "lalala";
#else
        /// <summary>
        /// The universally unique identifier string for client builds.
        /// The main goal is to securely verify client interactions.
        /// It is used automatically in RPC headers or authentication steps.
        /// </summary>
        public static string guid;
#endif
#if SERVER || UNITY_EDITOR
        /// <summary>
        /// The fallback environment identifier for server or editor builds.
        /// The main goal is to seamlessly transition development auth logic.
        /// It is used heavily in local testing via the editor.
        /// </summary>
        public static string guidue = "lalala";
#else
        /// <summary>
        /// The mapped identifier for non-editor client standalone builds.
        /// The main goal is to act as the runtime authentication token string.
        /// It is used dynamically depending on the current build macro.
        /// </summary>
        public static string guidue;
#endif
    }
}