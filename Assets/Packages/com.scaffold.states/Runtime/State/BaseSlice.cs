#nullable enable

using System;

namespace Scaffold.States
{
    /// <summary>
    /// Non-generic slice row for the store map keyed by <see cref="Reference"/> and <see cref="StateType"/>.
    /// </summary>
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

    /// <summary>
    /// Slice row whose committed value is constrained to <typeparamref name="T"/>.
    /// </summary>
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
