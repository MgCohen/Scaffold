using System.Threading;

namespace Scaffold.EffectGraph.Runtime
{
    /// <summary>
    /// Execution context carried per run. Consumers subclass this with game services and host references.
    /// </summary>
    public abstract class GraphRunner
    {
        public CancellationToken CancellationToken { get; init; }
    }
}
