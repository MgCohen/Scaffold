using System;
using System.Collections.Generic;

namespace Scaffold.AppFlow
{
    public interface IAppFlowErrorHandler
    {
        void Report(AppFlowErrorInfo info);

        void Report(string source, Exception exception);

        event Action<AppFlowErrorInfo> OnError;

        IReadOnlyList<AppFlowErrorInfo> Recent { get; }
    }
}
