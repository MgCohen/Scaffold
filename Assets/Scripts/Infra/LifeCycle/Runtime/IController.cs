namespace Scaffold.LifeCycle
{
    /// <summary>
    /// Serves as the base contract for system controllers that require both initialization and disposal capabilities.
    /// The main goal is to unify the <see cref="IInitialize"/> and <see cref="IDispose"/> interfaces into a single controller type.
    /// It is used primarily by modules that need structured instantiation and cleanup processes managed by the game loop.
    /// </summary>
    public interface IController : IInitialize, IDispose
    {

    }
}
