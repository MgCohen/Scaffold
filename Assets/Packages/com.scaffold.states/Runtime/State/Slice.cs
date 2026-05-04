#nullable enable

using System;

namespace Scaffold.States
{
    public sealed class Slice : BaseSlice<State>
    {
        public Slice(Reference reference, State state) : base(reference, state)
        {
        }

        public override void Set(State state)
        {
            State = state;
        }

        public static Slice Create(Reference? reference, State state)
        {
            reference ??= States.Reference.Null;
            return new Slice(reference, state);
        }
    }
}
