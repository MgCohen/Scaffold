using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scaffold.AppFlow
{
    public interface IAppFlowProgress
    {
        AppFlowSession Current { get; }

        event Action<AppFlowSession> Changed;

        Task<AppFlowOutcome> WhenSessionCompleted();

        IReadOnlyList<AppFlowSession> History { get; }
    }
}
