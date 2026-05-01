using VContainer;

namespace Scaffold.AppFlow
{
    public interface IScopeLayer
    {
        string Name => this.GetType().Name;

        void Install(IContainerBuilder builder);
    }
}
