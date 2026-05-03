using System.Threading;

namespace Scaffold.GraphFlow.M0
{
    /// <summary>M0 — services carrier for one graph execution (ExecPlan v2).</summary>
    public abstract class GraphRunner
    {
        public CancellationToken CancellationToken { get; set; }
    }
}
