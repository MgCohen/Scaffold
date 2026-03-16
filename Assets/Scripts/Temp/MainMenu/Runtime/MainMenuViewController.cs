using CommunityToolkit.Mvvm.ComponentModel;
using Madbox.GameEngine;
using Madbox.Meta.Gold;
using Madbox.Meta.Level;
using Scaffold.MVVM;
using Scaffold.MVVM.Binding;
using VContainer;

namespace Madbox.App.MainMenu
{
    public partial class MainMenuViewController : ViewModel
    {
        [Inject] private ILevelService levelService;
        [Inject] private IGoldService goldService;
        [Inject] private IGameEngine gameEngine;

        [ObservableProperty]
        private Game currentGame;

        [ObservableProperty]
        private string currentLevelLabel = "Level: N/A";

        [ObservableProperty]
        private string currentGoldLabel = "Gold: N/A";

        [ObservableProperty]
        private string levelStateLabel = "State: not started";

        [ObservableProperty]
        private bool canStartLevel = true;

        [ObservableProperty]
        private bool canFinishLevel;
        private IBindedProperty<GameState, GameState> stateBind;

        protected override void Initialize()
        {
            base.Initialize();
            CurrentLevelLabel = BuildLevelLabel();
            CurrentGoldLabel = BuildGoldLabel();

            stateBind?.Dispose();
            stateBind = Bind<GameState, GameState>(() => CurrentGame.State, SetState, BindingOptions.Lazy);
            ReplaceCurrentGame(CreateCurrentLevelGame());
        }

        protected override void OnClosed()
        {
            stateBind?.Dispose();
            stateBind = null;
            ReplaceCurrentGame(null);
            base.OnClosed();
        }

        public void StartLevel()
        {
            if (!CanStartNewLevel()) { return; }
            StartCurrentLevelGame();
        }

        public void FinishLevel()
        {
            if (!CanFinishLevel || CurrentGame == null)
            {
                return;
            }

            CurrentGame.Finish();
        }

        private string BuildLevelLabel()
        {
            if (levelService == null)
            {
                return "Level: N/A";
            }
            Level level = levelService.GetCurrentLevel();
            return $"Level: {level.Id}";
        }

        private string BuildGoldLabel()
        {
            if (goldService == null)
            {
                return "Gold: N/A";
            }
            int gold = goldService.GetCurrentGold();
            return $"Gold: {gold}";
        }

        private void OnGameCompleted(bool didWin)
        {
            if (didWin && levelService != null)
            {
                levelService.AdvanceToNextLevel();
            }

            ReplaceCurrentGame(null);
            CurrentLevelLabel = BuildLevelLabel();
        }

        private bool CanStartNewLevel()
        {
            return CanStartLevel && levelService != null && gameEngine != null;
        }

        private void StartCurrentLevelGame()
        {
            if (CurrentGame == null || CurrentGame.State == GameState.Finished)
            {
                ReplaceCurrentGame(CreateCurrentLevelGame());
            }

            CurrentGame?.Start();
        }

        private Game CreateCurrentLevelGame()
        {
            if (levelService == null || gameEngine == null)
            {
                return null;
            }

            Level level = levelService.GetCurrentLevel();
            return gameEngine.CreateGame(level);
        }

        private void ReplaceCurrentGame(Game game)
        {
            if (ReferenceEquals(CurrentGame, game))
            {
                return;
            }

            if (CurrentGame != null)
            {
                CurrentGame.Completed -= OnGameCompleted;
            }

            CurrentGame = game;

            if (CurrentGame != null)
            {
                CurrentGame.Completed += OnGameCompleted;
            }
        }

        partial void OnCurrentGameChanged(Game value)
        {
            RegisterChildProperty(nameof(CurrentGame), value);
        }

        private void SetState(GameState state)
        {
            LevelStateLabel = BuildStateLabel(state);
            CanStartLevel = state != GameState.Started;
            CanFinishLevel = state == GameState.Started;
        }

        private static string BuildStateLabel(GameState state)
        {
            return state switch
            {
                GameState.Initializing => "State: not started",
                GameState.Started => "State: started",
                _ => "State: done",
            };
        }
    }
}