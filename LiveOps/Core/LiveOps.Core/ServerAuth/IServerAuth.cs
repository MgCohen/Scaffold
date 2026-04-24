using System.Threading;
using System.Threading.Tasks;
using LiveOps.ModuleFetchData;
using Unity.Services.CloudCode.Core;

namespace LiveOps.ServerAuth
{
    /// <summary>
    /// Validates server-to-server or privileged calls (e.g. direct push) against game state–stored secrets.
    /// </summary>
    public interface IServerAuth
    {
        /// <param name="guid">The claimed shared secret to validate (e.g. a GUID string).</param>
        /// <returns>True when the secret matches the value stored in game state for this context.</returns>
        Task<bool> IsValidForServerAccessAsync(
            IGameState gameState,
            IExecutionContext context,
            string guid,
            CancellationToken cancellationToken = default);
    }
}
