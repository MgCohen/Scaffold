#nullable enable

namespace Scaffold.States
{
    internal interface IStoreScratchpad : IStateScope
    {
        void Commit();

        void SetPending<TState>(IReference? reference, TState state) where TState : State;

        void Reset();
    }
}
