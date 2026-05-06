#nullable enable

using System;

namespace Scaffold.States
{
    public abstract class BaseSlice
    {
        protected BaseSlice(Reference reference, BaseState state)
        {
            Reference = reference;
            State = state;
        }

        protected BaseSlice(Reference reference)
        {
            Reference = reference;
            State = null!;
        }

        public Reference Reference { get; }

        public BaseState State { get; protected set; }

        public virtual Type StateType => State.GetType();

        public virtual void Set(State state)
        {
            throw new NotSupportedException($"{GetType().Name} does not support replacing canonical state via Set.");
        }
    }

    public abstract class BaseSlice<T> : BaseSlice where T : BaseState
    {
        protected BaseSlice(Reference reference, T state) : base(reference, state)
        {

        }

        protected BaseSlice(Reference reference) : base(reference)
        {

        }
    }
}
