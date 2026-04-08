#nullable enable

using System;

namespace Scaffold.States
{
    public abstract class BaseSlice
    {
        protected BaseSlice(IReference reference, BaseState state)
        {
            Reference = reference;
            State = state;
        }

        protected BaseSlice(IReference reference)
        {
            Reference = reference;
            State = null!;
        }

        public IReference Reference { get; }

        public BaseState State { get; protected set; }

        public virtual Type StateType => State.GetType();

        public virtual void Set(State state)
        {
            throw new NotSupportedException($"{GetType().Name} does not support replacing canonical state via Set.");
        }
    }

    public abstract class BaseSlice<T> : BaseSlice where T : BaseState
    {
        protected BaseSlice(IReference reference, T state) : base(reference, state)
        {

        }

        protected BaseSlice(IReference reference) : base(reference)
        {

        }
    }
}
