#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Terminal state of a single <c>controller.Run</c> invocation. Read off the <see cref="Flow"/>
    /// returned by the executor. <see cref="Stopped"/> = walked to a leaf (or a node that called
    /// <see cref="Flow.Stop"/>); <see cref="Returned"/> = a <c>Return&lt;,&gt;</c> terminator wrote a
    /// value into <see cref="Flow.Result"/>; <see cref="Cancelled"/> = a <c>Cancel</c> terminator
    /// (or any other node that called <see cref="Flow.Cancel"/>) halted the walk.
    /// </summary>
    public enum FlowOutcome { Stopped, Returned, Cancelled }

    /// <summary>
    /// Per-run state object. Constructed at the start of each <c>controller.Run</c>, plumbed through
    /// every <c>RuntimeNode.Execute</c> on the walk, and discarded when the walk ends. Two concurrent
    /// <c>Run</c> calls produce two <see cref="Flow"/> instances and never share state — the
    /// <see cref="GraphRunner"/> is the long-lived services carrier and stays clean across runs.
    ///
    /// <para>Authors mutate the flow via <see cref="GoTo"/> / <see cref="Stop"/> / <see cref="Return"/> /
    /// <see cref="Cancel"/>; all four return <see cref="Task.CompletedTask"/> so a one-line
    /// <c>Execute</c> body reads <c>return flow.GoTo("MyOutPort");</c>.</para>
    /// </summary>
    public sealed class Flow
    {
        public CancellationToken CancellationToken { get; }
        public string? Reason { get; set; }

        public FlowOutcome Outcome { get; private set; }
        internal object? Result { get; private set; }
        string? _nextPortName;

        /// <summary>
        /// Per-run host-services bag. Mode-2 runners (e.g. CardEffectRunner) populate this in
        /// GraphController's BindRunner closure so dispatcher nodes can reach the host's services
        /// without resurrecting state on the long-lived runner. Mode-1 runners can leave it null.
        /// </summary>
        public IEffectScope? Scope { get; internal set; }

        /// <summary>
        /// Runner-agnostic access to the active <see cref="GraphRunner"/>. Set by <c>GraphExecutor</c>
        /// at run start (mirror of <see cref="Scope"/>). Typed-runner nodes prefer the cached
        /// <c>RuntimeNode&lt;TRunner&gt;._runner</c> field — this property is for runner-agnostic
        /// nodes that need untyped access during Execute.
        /// </summary>
        public GraphRunner? Runner { get; internal set; }

        public Flow(CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
        }

        /// <summary>Follow the flow-out port with the given name. The executor reads this after Execute returns.</summary>
        public Task GoTo(string outFlowPortName)
        {
            _nextPortName = outFlowPortName;
            return Task.CompletedTask;
        }

        /// <summary>Stop the walk at this node — terminal leaf or no further routing.</summary>
        public Task Stop()
        {
            Outcome = FlowOutcome.Stopped;
            _nextPortName = null;
            return Task.CompletedTask;
        }

        /// <summary>Stop the walk and record a typed return value readable through <c>flow.ReadResult&lt;T&gt;()</c>.</summary>
        public Task Return<T>(T value)
        {
            Outcome = FlowOutcome.Returned;
            Result = value;
            _nextPortName = null;
            return Task.CompletedTask;
        }

        /// <summary>Stop the walk and mark the run as cancelled.</summary>
        public Task Cancel()
        {
            Outcome = FlowOutcome.Cancelled;
            _nextPortName = null;
            return Task.CompletedTask;
        }

        internal string? ConsumeNext()
        {
            var n = _nextPortName;
            _nextPortName = null;
            return n;
        }

        public T? ReadResult<T>() => Result is T t ? t : default;
    }
}
