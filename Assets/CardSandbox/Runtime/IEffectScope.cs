#nullable enable
using System.Threading;

using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Per-execution scope handed to commands and listeners. Stand-in for the real Card Framework
    /// IEffectScope — just enough surface to validate the M3 graph integration loop:
    /// host references via <see cref="Get{T}"/>, cancellation, and a one-line reason for traces.
    /// </summary>
    public interface IEffectScope
    {
        CancellationToken CancellationToken { get; }

        /// <summary>Look up a host service registered on the scope (audio, animation, etc.).</summary>
        T? Get<T>() where T : class;

        /// <summary>Free-form trace string for asserts and logs.</summary>
        string Reason { get; }
    }
}
