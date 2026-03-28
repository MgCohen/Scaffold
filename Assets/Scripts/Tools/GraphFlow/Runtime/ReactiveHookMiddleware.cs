using System;
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public sealed class ReactiveHookMiddleware : IGraphMiddleware
    {
        public async ValueTask InvokeAsync(MiddlewareContext context, Func<ValueTask> next)
        {
            foreach (var hook in context.Graph.ReactiveHooks)
            {
                if (hook.Timing != context.Phase)
                    continue;
                if (!ReferenceEquals(hook.TargetDefinition, context.CurrentNode.Definition))
                    continue;

                var child = context.Flow.CreateChild();
                child.ReactivePayload = context.Instance;
                var entry = new ReactiveChildEntry(context.Instance);
                await context.Runner.RunChildGraphAsync(hook.ReactiveSubgraph, entry, child, context.Flow.Cancellation)
                    .ConfigureAwait(false);
            }

            await next().ConfigureAwait(false);
        }
    }

    public sealed class ReactiveChildEntry : GraphEntryPoint
    {
        public ReactiveChildEntry(object contextPayload) => ContextPayload = contextPayload;

        public object ContextPayload { get; }
    }
}
