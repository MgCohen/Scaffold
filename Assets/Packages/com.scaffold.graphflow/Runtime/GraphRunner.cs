#nullable enable
using System.Threading;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Session-long services carrier for one graph runner. Reused across many <c>controller.Run</c>
    /// invocations — per-run state (outcome, return value, cancellation specifics) lives on
    /// <see cref="Flow"/>, not here. Subclasses add host-specific service references on top.
    /// </summary>
    public abstract class GraphRunner
    {
        public CancellationToken CancellationToken { get; set; }
    }
}
