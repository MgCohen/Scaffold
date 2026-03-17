using Scaffold.Infra.CloudGateway;

namespace Scaffold.Game.CloudGateway
{
    /// <summary>
    /// Stores the authentication keys required to bypass or validate module interactions.
    /// The main goal is to hold static references to access identifiers used by backend services.
    /// It is used during the network handshake or API queries to assert the client's authority.
    /// </summary>
    //TODO: Refactor
    public class CloudGatewayAuthKey : ICloudGatewayAuthKey
    {
#if SERVER
        public static string Guid { get; } = "lalala";
#else
        public static string Guid { get; }
#endif
#if SERVER || UNITY_EDITOR
        public static string Guidue { get; } = "lalala";
#else
        public static string Guidue { get; }
#endif
    }
}