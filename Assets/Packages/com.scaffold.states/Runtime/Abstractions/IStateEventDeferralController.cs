#nullable enable
using System;

namespace Scaffold.States
{
    public interface IStateEventDeferralController
    {
        void Flush();

        IDisposable BeginDeferScope();
    }
}
