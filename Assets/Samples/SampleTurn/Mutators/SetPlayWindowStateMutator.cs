using Scaffold.States;

namespace Sample.Turn.Mutators
{
    /// <summary>
    /// Replaces the PlayWindowState slice; used by IPlayWindowContext.SetWindowState.
    /// </summary>
    public class SetPlayWindowStateMutator : Mutator<PlayWindowState>
    {
        private readonly PlayWindowState _windowState;

        public SetPlayWindowStateMutator(PlayWindowState windowState)
        {
            _windowState = windowState;
        }

        public override PlayWindowState Change(PlayWindowState state)
        {
            return _windowState;
        }
    }
}
