#nullable enable
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>OnPlay imperative entry payload. Host invokes via controller.Run.</summary>
    [GraphEntry]
    public sealed class OnPlay : IGraphEntry
    {
        public object? Target;
    }
}
