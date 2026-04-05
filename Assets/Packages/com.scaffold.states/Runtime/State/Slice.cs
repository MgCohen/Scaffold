#nullable enable

using System;

namespace Scaffold.States
{
    /// <summary>
    /// Canonical slice row: committed <see cref="State"/> values are replaced by mutators or snapshot load.
    /// </summary>
    public sealed class Slice : BaseSlice<State>
    {
        public Slice(IReference reference, State state) : base(reference, state)
        {
        }

        public override void Set(State state)
        {
            State = state;
        }

        public static Slice Create(IReference? reference, State state)
        {
            reference ??= States.Reference.Null;
            return new Slice(reference, state);
        }
    }
}
