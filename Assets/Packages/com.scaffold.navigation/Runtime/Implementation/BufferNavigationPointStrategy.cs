using Scaffold.Navigation.Contracts;

namespace Scaffold.Navigation
{
    internal sealed class BufferNavigationPointStrategy : INavigationPointStrategy
    {
        public BufferNavigationPointStrategy(NavigationViewInstanceBuffer instanceBuffer)
        {
            this.instanceBuffer = instanceBuffer;
        }

        private readonly NavigationViewInstanceBuffer instanceBuffer;

        public bool TryCreate(ViewConfig config, IViewController controller, NavigationOptions options, out NavigationPoint point)
        {
            if (!instanceBuffer.TryTake(config, out IView cachedView))
            {
                point = null;
                return false;
            }

            point = new NavigationPoint(cachedView, controller, config, false, options, disposed => { if (disposed == null || disposed.IsSceneView || disposed.View == null) return; instanceBuffer.Return(disposed.Config, disposed.View); });
            return true;
        }
    }
}
