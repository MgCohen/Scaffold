using System;
using System.Threading;

namespace Scaffold.GraphFlow
{
    public sealed class NullGraphTickService : IGraphTickService
    {
        public CancellationToken Cancellation => CancellationToken.None;

        public void Register(object owner, Action callback) { }

        public void Unregister(object owner) { }
    }
}
