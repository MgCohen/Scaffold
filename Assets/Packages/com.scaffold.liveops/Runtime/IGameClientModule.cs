namespace Scaffold.LiveOps
{
    /// <summary>
    /// Client-side game module marker registered in DI (for example as <c>IGameClientModule</c>).
    /// </summary>
    public interface IGameClientModule
    {
        string Key { get; }
    }
}
