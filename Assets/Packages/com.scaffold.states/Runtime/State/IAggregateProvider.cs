#nullable enable

using System;

namespace Scaffold.States
{
    /// <summary>
    /// Aggregation policy for one aggregate row: canonical subscriptions and folding <see cref="IStateScope"/> into
    /// the committed <see cref="AggregateState"/> instance. <see cref="Build"/> is the sole source for the row value
    /// (first when the aggregate slice attaches to the store, then on each rebuild). Prefer deriving <see cref="AggregateProvider{T}"/>.
    /// </summary>
    public interface IAggregateProvider
    {
        Type AggregateStateType { get; }

        void Wire(IStoreScope scope, IAggregateRebuild rebuild);

        BaseState Build(IStateScope scope);
    }
}
