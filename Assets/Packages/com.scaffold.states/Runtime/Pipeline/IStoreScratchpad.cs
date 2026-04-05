#nullable enable

namespace Scaffold.States
{
    /// <summary>
    /// Overlay-first read scope for mutator execution: owns pending snapshot merge logic and
    /// committed-key discovery via the owning <see cref="Store"/> (no separate enumeration API on <see cref="Store"/>).
    /// </summary>
    internal interface IStoreScratchpad : IStateScope
    {
        void Commit();

        void SetPending<TState>(IReference? reference, TState state) where TState : State;
    }
}
