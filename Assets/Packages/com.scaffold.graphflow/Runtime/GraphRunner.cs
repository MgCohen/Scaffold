#nullable enable
using System.Threading;

namespace Scaffold.GraphFlow
{
    /// <summary>M0 — services carrier for one graph execution (ExecPlan v2).</summary>
    public abstract class GraphRunner
    {
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Set by the <c>Cancel</c> generic node when a flow walks into it. Read by callers after
        /// <c>controller.Run</c> completes to distinguish "ran to a Cancel terminator" from
        /// "ran to a Return terminator". The strongly-typed return-value channel hangs off
        /// <see cref="ReturnValue"/>; the typed wrapper lands with Mode 2 / [Return Strike] in M3.
        /// </summary>
        public bool Cancelled { get; set; }

        /// <summary>
        /// Set by the <c>Return</c> / <c>ReturnBool</c> generic nodes. <see cref="Return"/> writes
        /// <c>null</c>; <see cref="ReturnBool"/> writes the upstream bool. M3's typed-payload return
        /// (e.g. <c>Return&lt;TCmd&gt;</c> for Mode 2) replaces this with a generic-runner overload.
        /// </summary>
        public object? ReturnValue { get; set; }
    }
}
