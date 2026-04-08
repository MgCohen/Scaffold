using VContainer;
using VContainer.Unity;

namespace Scaffold.Pooling.Container
{
    /// <summary>
    /// Placeholder installer. Register concrete <see cref="Pool{T}"/> instances at the application composition root.
    /// </summary>
    public sealed class PoolingInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
        }
    }
}
