using System.Collections.Generic;
using System.Linq;
using Scaffold.States;

namespace Sample.Turn.Mutators
{
    /// <summary>
    /// Pushes a play window onto the stack and adds its initial state to the dictionary.
    /// </summary>
    public class PushPlayWindowMutator : Mutator<PlayState>
    {
        private readonly PlayWindow _window;

        public PushPlayWindowMutator(PlayWindow window)
        {
            _window = window;
        }

        public override PlayState Change(PlayState state)
        {
            var newStack = state.WindowStack != null ? state.WindowStack.ToList() : new List<PlayWindow>();
            newStack.Add(_window);
            var newDict = state.WindowStates != null ? new Dictionary<PlayWindow, PlayWindowState>(state.WindowStates) : new Dictionary<PlayWindow, PlayWindowState>();
            //if (_initialState != null)
            //    newDict[_window] = _initialState;
            return new PlayState(newStack, newDict);
        }
    }
}
