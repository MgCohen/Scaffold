using System;
using System.Threading.Tasks;
using LiveOps.DTO.ModuleRequest;

namespace LiveOps.GameApi
{
    /// <summary>
    /// Non-generic GameApi handler contract used by <see cref="GameApiDispatcher"/> for reflection-free dispatch.
    /// </summary>
    public interface IGameApiHandler
    {
        Type RequestType { get; }

        Task<ModuleResponse> HandleAsync(GameApiSession session, object request);

        /// <summary>
        /// <c>null</c>: warm full player snapshot. Empty: skip player prefetch. Otherwise: keys for future selective fetch (currently full snapshot).
        /// </summary>
        string[]? PlayerKeys() => null;

        /// <summary>
        /// <c>null</c>: warm full remote config. Empty: skip config prefetch. Otherwise: keys for future selective fetch (currently full snapshot).
        /// </summary>
        string[]? ConfigKeys() => null;
    }

    /// <summary>
    /// Handles a single <see cref="ModuleRequest{TResponse}"/> type in the GameApi pipeline.
    /// </summary>
    public interface IGameApiHandler<TRequest, TResponse> : IGameApiHandler
        where TRequest : ModuleRequest<TResponse>
        where TResponse : ModuleResponse
    {
        Task<TResponse> HandleAsync(GameApiSession session, TRequest request);

        Type IGameApiHandler.RequestType => typeof(TRequest);

        async Task<ModuleResponse> IGameApiHandler.HandleAsync(GameApiSession session, object request)
        {
            if (request is not TRequest typed)
            {
                throw new InvalidOperationException(
                    $"GameApi handler for {typeof(TRequest).Name} received {request?.GetType().Name ?? "null"}.");
            }

            return await HandleAsync(session, typed).ConfigureAwait(false);
        }
    }
}
