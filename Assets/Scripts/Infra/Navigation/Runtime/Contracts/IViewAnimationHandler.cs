using UnityEngine;

namespace Scaffold.Navigation
{
    public interface IViewAnimationHandler
    {
        Awaitable AnimateView(AnimationType direction);
    }
}
