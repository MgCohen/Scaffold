using System.Threading;
using System.Threading.Tasks;
using LiveOps.DTO.GameModule;
using LiveOps.GameApi;

namespace LiveOps.GameModule
{
    /// <summary>
    /// Strongly-typed abstract base for game modules mapping a specific data type.
    /// </summary>
    /// <typeparam name="T">The module data type constrained to IGameModuleData implementations.</typeparam>
    public abstract class GameModule<T> : IGameModule where T : IGameModuleData
    {
        /// <summary>Gets the component key mapped dynamically.</summary>
        public string Key => typeof(T).Name;

        /// <inheritdoc />
        public abstract Task<IGameModuleData> InitializeAsync(
            GameApiSession session,
            CancellationToken cancellationToken = default);
    }
}