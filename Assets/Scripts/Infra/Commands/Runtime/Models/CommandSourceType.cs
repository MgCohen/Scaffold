namespace Scaffold.Commands
{
    /// <summary>
    /// Source categories used by transport and queue stream identity.
    /// </summary>
    public enum CommandSourceType
    {
        Unknown = 0,
        Local = 1,
        Server = 2,
        Client = 3,
        System = 4
    }
}
