using System.Threading.Tasks;

namespace Scaffold.Navigation
{
    public interface IViewAnimationHandler
    {
        Task AnimateView(AnimationType direction);
    }
}
