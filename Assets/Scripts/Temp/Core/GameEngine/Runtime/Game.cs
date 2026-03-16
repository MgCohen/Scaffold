using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Madbox.Meta.Level;
using Scaffold.MVVM;

namespace Madbox.GameEngine
{
    public partial class Game : Model
    {
        public Game(Level levelValue)
        {
            if (levelValue == null)
            {
                throw new ArgumentNullException(nameof(levelValue));
            }

            level = levelValue;
            State = GameState.Initializing;
        }

        public LevelId LevelId => level.Id;

        private readonly Level level;
        
        [ObservableProperty]
        private GameState state;

        public event Action<bool> Completed;

        public void Start()
        {
            EnsureCanStart();
            State = GameState.Started;
        }

        public void Finish(bool didWin = true)
        {
            if (State == GameState.Initializing) throw new InvalidOperationException("Game has not started yet.");
            if (State == GameState.Finished) return;
            Complete(didWin);
        }

        private void EnsureCanStart()
        {
            if (State == GameState.Started) throw new InvalidOperationException("Game has already started.");
            if (State == GameState.Finished) throw new InvalidOperationException("Game is already completed.");
        }

        private void Complete(bool didWin)
        {
            State = GameState.Finished;
            Completed?.Invoke(didWin);
        }
    }
}
