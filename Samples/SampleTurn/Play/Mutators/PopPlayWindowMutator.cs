using System.Collections.Generic;
using System.Linq;
using Scaffold.States;

namespace Sample.Turn.Mutators
{
    /// <summary>
    /// Pops the top play window from the stack and removes its state from the dictionary.
    /// </summary>
    public class PopPlayWindowMutator : Mutator<PlayState>
    {
        public override PlayState Change(PlayState state)
        {
            if (state.WindowStack == null || state.WindowStack.Count == 0)
                return state;
            var newStack = state.WindowStack.Take(state.WindowStack.Count - 1).ToList();
            var topWindow = state.WindowStack[state.WindowStack.Count - 1];
            var newDict = state.WindowStates != null ? new Dictionary<PlayWindow, PlayWindowState>(state.WindowStates) : new Dictionary<PlayWindow, PlayWindowState>();
            newDict.Remove(topWindow);
            return new PlayState(newStack, newDict);
        }
    }
}
