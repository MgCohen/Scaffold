using System.Collections.Generic;
using Scaffold.States;

namespace Sample.Turn.Mutators
{
    /// <summary>
    /// Updates the state for a specific play window in the dictionary.
    /// </summary>
    public class UpdatePlayWindowStateMutator : Mutator<PlayState>
    {
        private readonly PlayWindow _window;
        private readonly PlayWindowState _windowState;

        public UpdatePlayWindowStateMutator(PlayWindow window, PlayWindowState windowState)
        {
            _window = window;
            _windowState = windowState;
        }

        public override PlayState Change(PlayState state)
        {
            var newDict = state.WindowStates != null ? new Dictionary<PlayWindow, PlayWindowState>(state.WindowStates) : new Dictionary<PlayWindow, PlayWindowState>();
            newDict[_window] = _windowState;
            return new PlayState(state.WindowStack, newDict);
        }
    }
}
