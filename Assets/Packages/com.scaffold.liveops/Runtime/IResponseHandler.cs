using System;
using System.Collections.Generic;
using LiveOps.Core.DTO.ModuleRequest;

namespace Scaffold.LiveOps
{
    public interface IResponseHandler
    {
        Type HandledResponseType { get; }

        void Handle(ModuleResponse response);
    }
}
