#nullable enable
using Scaffold.States;

namespace Scaffold.Entities.States
{
    // Internal contract used by StoreVariableBagBuilder to fan out a single
    // store-side subscription to every handle bound against the same
    // (Reference, TState) slice. Handles cast the incoming state to their own
    // TState in their Prime / OnSliceChanged overrides.
    internal interface ISliceListener<TState> where TState : State
    {
        void Prime(TState state);
        void OnSliceChanged(TState state);
    }
}
