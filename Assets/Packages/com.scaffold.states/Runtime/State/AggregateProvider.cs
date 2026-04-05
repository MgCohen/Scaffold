#nullable enable

using System;

namespace Scaffold.States
{
    /// <summary>
    /// Base for aggregate providers: implements <see cref="IAggregateProvider"/> while subclasses implement
    /// <see cref="BuildCore"/> with a concrete <typeparamref name="TAggregate"/>. The committed row value is always
    /// produced by <see cref="Build"/> (first on slice attach, then on rebuild).
    /// </summary>
    public abstract class AggregateProvider<TAggregate> : IAggregateProvider where TAggregate : AggregateState
    {
        public Type AggregateStateType => typeof(TAggregate);

        public BaseState Build(IStateScope scope)
        {
            return BuildCore(scope);
        }

        public abstract void Wire(IStoreScope scope, IAggregateRebuild rebuild);

        protected abstract TAggregate BuildCore(IStateScope scope);
    }
}
