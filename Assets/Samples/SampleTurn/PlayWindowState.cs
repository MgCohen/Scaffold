using Scaffold.States;

namespace Sample.Turn
{
    /// <summary>
    /// Base record for window-specific state. Each PlayWindow subclass defines its own concrete state extending this.
    /// </summary>
    public record PlayWindowState() : State;
}
