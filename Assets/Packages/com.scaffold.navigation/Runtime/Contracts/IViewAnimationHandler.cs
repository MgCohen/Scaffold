using System.Threading.Tasks;

namespace Scaffold.Navigation.Contracts
{
    public interface IViewAnimationHandler
    {
        Task AnimateView(AnimationType direction);
    }
}




