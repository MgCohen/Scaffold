using UnityEngine;

namespace Scaffold.Navigation
{
    public interface IViewTransitionHandler
    {
        Awaitable DoTransition(ViewTransitionData transitionData, TransitionDirection direction);
    }
}
