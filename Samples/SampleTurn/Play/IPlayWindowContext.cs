namespace Sample.Turn
{
    /// <summary>
    /// Provided to registered execute handlers so the window can read/update its state and control play flow without knowing Store internals.
    /// </summary>
    public interface IPlayWindowContext
    {
        TState GetWindowState<TState>() where TState : PlayWindowState;
        void SetWindowState(PlayWindowState state);
        void CloseWindow();
    }
}
