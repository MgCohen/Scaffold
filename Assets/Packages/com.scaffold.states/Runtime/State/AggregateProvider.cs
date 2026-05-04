#nullable enable

using System;

namespace Scaffold.States
{
    public abstract class AggregateProvider<TAggregate> : IAggregateProvider where TAggregate : AggregateState
    {
        public Type AggregateStateType => typeof(TAggregate);

        public BaseState Build(IStateScope scope)
        {
            return BuildCore(scope);
        }

        public abstract IDisposable Wire(IStoreScope scope, IAggregateRebuild rebuild);

        protected abstract TAggregate BuildCore(IStateScope scope);
    }
}
