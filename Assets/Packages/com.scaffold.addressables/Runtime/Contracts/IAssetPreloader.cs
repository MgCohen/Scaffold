using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.Addressables.Contracts
{
    public interface IAssetPreloader
    {
        Task PreloadAsync(CancellationToken cancellationToken);
    }
}
