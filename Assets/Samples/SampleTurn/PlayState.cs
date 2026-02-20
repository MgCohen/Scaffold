using Scaffold.States;

namespace Sample.Turn
{
    /// <summary>
    /// Tracks which play window is currently open. Window-specific state lives in PlayWindowState.
    /// </summary>
    public record PlayState(PlayWindow CurrentPlayWindow) : State;
}
