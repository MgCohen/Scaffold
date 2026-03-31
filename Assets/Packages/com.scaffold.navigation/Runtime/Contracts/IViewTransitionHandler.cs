using System.Threading.Tasks;

namespace Scaffold.Navigation.Contracts
{
    public interface IViewTransitionHandler
    {
        Task DoTransition(object transitionData, TransitionDirection direction);
    }
}




