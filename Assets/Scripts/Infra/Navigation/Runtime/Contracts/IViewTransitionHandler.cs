using System.Threading.Tasks;

namespace Scaffold.Navigation
{
    public interface IViewTransitionHandler
    {
        Task DoTransition(ViewTransitionData transitionData, TransitionDirection direction);
    }
}
