using System.Threading;
using System.Threading.Tasks;
using LiveOps.DTO.GameModule;
using LiveOps.GameApi;

namespace LiveOps.GameModule
{
    /// <summary>
    /// Provides the base contract for initializing independent game module instances.
    /// </summary>
    public interface IGameModule
    {
        /// <summary>Gets the unique authorization mapping identification.</summary>
        public string Key { get; }

        /// <summary>
        /// Populates the module from caches exposed on <see cref="GameApiSession" />.
        /// </summary>
        Task<IGameModuleData> InitializeAsync(
            GameApiSession session,
            CancellationToken cancellationToken = default);

        /// <summary><c>null</c>: warm full player snapshot; empty: skip; else keys (selective fetch TBD).</summary>
        string[]? PlayerKeys() => null;

        /// <summary><c>null</c>: warm full remote config; empty: skip; else keys (selective fetch TBD).</summary>
        string[]? ConfigKeys() => null;
    }
}