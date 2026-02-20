using Scaffold.States;

namespace Sample.Turn.Mutators
{
    /// <summary>
    /// Sets the current play window when a window is opened or closed.
    /// </summary>
    public class SetPlayWindowMutator : Mutator<PlayState>
    {
        private readonly PlayWindow _window;

        public SetPlayWindowMutator(PlayWindow window)
        {
            _window = window;
        }

        public override PlayState Change(PlayState state)
        {
            return state with { CurrentPlayWindow = _window };
        }
    }
}
