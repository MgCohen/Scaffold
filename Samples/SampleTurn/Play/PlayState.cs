using System.Collections.Generic;
using Scaffold.States;

namespace Sample.Turn
{
    /// <summary>
    /// Tracks the stack of open play windows and each window's state. Last in stack is the active (top) window.
    /// </summary>
    public record PlayState(IReadOnlyList<PlayWindow> WindowStack, IReadOnlyDictionary<PlayWindow, PlayWindowState> WindowStates) : State;
}
