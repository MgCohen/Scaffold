#nullable enable

namespace Scaffold.States
{
    internal interface IStoreScratchpad : IStateScope
    {
        void Commit();

        void SetPending<TState>(Reference? reference, TState state) where TState : State;

        void Reset();
    }
}
