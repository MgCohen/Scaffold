using System;
using System.Threading;

namespace Scaffold.GraphFlow
{
    public interface IGraphTickService
    {
        CancellationToken Cancellation { get; }

        void Register(object owner, Action callback);

        void Unregister(object owner);
    }
}
