using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// The void-graph result placeholder. Used as <c>TResult</c> on entries that don't return a value
    /// (most card effects, action-style payloads). Lives in the package runtime asm so generated code,
    /// payloads, and host-side trigger wiring can all see it.
    /// </summary>
    public readonly struct Unit { public static readonly Unit Default = default; }

    /// <summary>
    /// Marker: typed graph entry payload. The generator reads <c>TPayload</c> for routing and
    /// <c>TResult</c> for the entry's return shape. One TResult per graph (validated by EFG-V07).
    /// </summary>
    public interface IGraphEntry<TPayload, TResult> { }

    /// <summary>Sugar for the void-graph case (TResult = Unit). Most card effects use this form.</summary>
    public interface IGraphEntry<TPayload> : IGraphEntry<TPayload, Unit> { }

    /// <summary>
    /// Trigger subset — host treats these as auto-subscribable to events of <c>TEvent</c>. Same node
    /// shape as <see cref="IGraphEntry{TPayload}"/>; the difference is purely how the host invokes it.
    /// </summary>
    public interface IGraphTrigger<TEvent> : IGraphEntry<TEvent, Unit> { }

    /// <summary>Marker: command/action-shaped payloads (Mode 1). Stays runner-typed.</summary>
    public interface IGraphAction<TRunner> where TRunner : GraphRunner { }

    /// <summary>Optional: payload executes itself instead of DispatcherBase (Mode 1).</summary>
    public interface IExecutable<TRunner> where TRunner : GraphRunner
    {
        Task Execute(TRunner runner);
    }
}
