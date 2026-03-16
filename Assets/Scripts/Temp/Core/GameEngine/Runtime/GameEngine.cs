using System;
using Madbox.Meta.Level;

namespace Madbox.GameEngine
{
    public sealed class GameEngine : IGameEngine
    {
        public Game CreateGame(Level level)
        {
            EnsureLevel(level);
            return new Game(level);
        }

        private void EnsureLevel(Level level)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }
        }
    }
}
