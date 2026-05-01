using System;
using LiveOps.DTO.ModuleRequest;

namespace Scaffold.LiveOps
{
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
