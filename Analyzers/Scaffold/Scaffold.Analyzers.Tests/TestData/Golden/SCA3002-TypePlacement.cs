
namespace Scaffold.GameEngine
{
    internal enum GameState
    {
        Initializing,
        Started,
        Finished
    }

    public sealed class Game
    {
        private GameState state;
    }
}
