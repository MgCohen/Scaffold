namespace Scaffold.NetworkMessages
{
    /// <summary>
    /// Logical source categories that identify where commands originate.
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
