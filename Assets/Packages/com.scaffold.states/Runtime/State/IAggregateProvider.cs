#nullable enable

using System;

namespace Scaffold.States
{
    public interface IAggregateProvider
    {
        Type AggregateStateType { get; }

        void Wire(IStoreScope scope, IAggregateRebuild rebuild);

        BaseState Build(IStateScope scope);
    }
}
