using Madbox.Meta.Level;

namespace Madbox.GameEngine
{
    public interface IGameEngine
    {
        Game CreateGame(Level level);
    }
}
