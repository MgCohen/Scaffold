using System;
using System.Threading.Tasks;
using LiveOps.DTO.ModuleRequest;

namespace LiveOps.GameApi
{

    public interface IGameApiHandler
    {
        Type RequestType { get; }

        Task<ModuleResponse> HandleAsync(GameApiSession session, object request);

        string[]? PlayerKeys() => null;

        string[]? ConfigKeys() => null;
    }

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
