using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.Scope.Contracts
{
    /// <summary>
    /// Types registered as this interface run <see cref="InitializeAsync"/> after the container is built,
    /// in dependency-derived waves (parallel within a wave). No manual ordering.
    /// </summary>
    public interface IAsyncInitializable
    {
        Task InitializeAsync(CancellationToken cancellationToken);
    }
}
