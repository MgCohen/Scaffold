using System.Threading;
using System.Threading.Tasks;
using VContainer;

namespace Scaffold.Scope.Contracts
{
    /// <summary>
    /// Runs <see cref="IAsyncInitializable.InitializeAsync"/> in topological levels: parallel within a level, await between levels.
    /// </summary>
    public interface IAsyncInitializationRunner
    {
        Task RunAsync(IObjectResolver resolver, CancellationToken cancellationToken);
    }
}
