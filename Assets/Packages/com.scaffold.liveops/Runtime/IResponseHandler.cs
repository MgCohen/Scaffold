using System;
using System.Collections.Generic;
using GameModuleDTO.ModuleRequests;

namespace Scaffold.LiveOps
{
    public interface IResponseHandler
    {
        Type HandledResponseType { get; }

        void Handle(ModuleResponse response);
    }
}
