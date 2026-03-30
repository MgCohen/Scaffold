using System;
using GameModuleDTO.ModuleRequests;

namespace Scaffold.LiveOps
{
    /// <summary>
    /// Handles a nested <see cref="ModuleResponse"/> of type <typeparamref name="T"/> delivered inside
    /// <see cref="ModuleResponse.Responses"/> after a LiveOps call completes.
    /// </summary>
    /// <typeparam name="T">Concrete nested response type.</typeparam>
    public interface IResponseHandler<in T> : IResponseHandler where T : ModuleResponse
    {
        Type IResponseHandler.HandledResponseType => typeof(T);

        void IResponseHandler.Handle(ModuleResponse response)
        {
            if (response is T typed)
            {
                Handle(typed);
            }
        }

        void Handle(T response);
    }
}
