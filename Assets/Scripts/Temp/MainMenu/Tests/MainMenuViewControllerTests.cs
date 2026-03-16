using Madbox.GameEngine;
using Madbox.Meta.Gold;
using Madbox.Meta.Level;
using NUnit.Framework;
using Scaffold.Navigation;
using UnityEngine;
using UnityEngine.UI;

namespace Madbox.App.MainMenu.Tests
{
    public class MainMenuViewControllerTests
    {
        [Test]
        public void Bind_FirstTime_DoesNotThrow_WhenButtonsAreAssigned()
        {
            using ViewHarness harness = CreateMainMenuViewHarness();
            BindViewWithoutThrowing(harness.View);
        }

        private ViewHarness CreateMainMenuViewHarness()
        {
            GameObject root = new GameObject(nameof(Bind_FirstTime_DoesNotThrow_WhenButtonsAreAssigned));
            MainMenuView view = root.AddComponent<MainMenuView>();
            Button startButton = CreateButton("StartButton");
            Button finishButton = CreateButton("FinishButton");
            AssignButtons(view, startButton, finishButton);
            return new ViewHarness(root, startButton.gameObject, finishButton.gameObject, view);
        }

        private static Button CreateButton(string name)
        {
            GameObject buttonObject = new GameObject(name);
            return buttonObject.AddComponent<Button>();
        }

        private void AssignButtons(MainMenuView view, Button startButton, Button finishButton)
        {
            AssignPrivateField(view, "startLevelButton", startButton);
            AssignPrivateField(view, "finishLevelButton", finishButton);
        }

        private void BindViewWithoutThrowing(MainMenuView view)
        {
            MainMenuViewController controller = CreateController();
            FakeNavigation navigation = new FakeNavigation();
            controller.Bind(navigation);
            Assert.DoesNotThrow(() => view.Bind(controller));
        }

        [Test]
        public void Bind_InitializesNotStartedState()
        {
            MainMenuViewController controller = CreateController();
            FakeNavigation navigation = new FakeNavigation();
            controller.Bind(navigation);

            Assert.AreEqual("State: not started", controller.LevelStateLabel);
            Assert.IsTrue(controller.CanStartLevel);
            Assert.IsFalse(controller.CanFinishLevel);
        }

        [Test]
        public void StartLevel_SetsStartedState()
        {
            MainMenuViewController controller = CreateController();
            FakeNavigation navigation = new FakeNavigation();
            controller.Bind(navigation);
            controller.StartLevel();
            AssertStartedState(controller);
        }

        [Test]
        public void FinishLevel_SetsDoneState()
        {
            MainMenuViewController controller = CreateController();
            StartLevel(controller);

            controller.FinishLevel();

            AssertDoneState(controller);
        }

        [Test]
        public void GameFinishedExternally_UpdatesDoneState()
        {
            RecordingGameEngine gameEngine = new RecordingGameEngine();
            MainMenuViewController controller = CreateController(gameEngine);
            StartLevel(controller);
            gameEngine.LastCreatedGame.Finish();
            AssertDoneState(controller);
        }

        [Test]
        public void StartLevel_AfterDone_UsesNextCurrentLevel()
        {
            RecordingGameEngine gameEngine = new RecordingGameEngine();
            MainMenuViewController controller = CreateController(gameEngine);
            StartLevel(controller);
            controller.FinishLevel();
            controller.StartLevel();

            Assert.AreEqual("L2", gameEngine.LastCreatedLevelId);
        }

        private MainMenuViewController CreateController(RecordingGameEngine gameEngine = null)
        {
            RecordingGameEngine engine = gameEngine ?? new RecordingGameEngine();
            MainMenuViewController controller = new MainMenuViewController();
            ILevelService levelService = BuildLevelService();
            IGoldService goldService = BuildGoldService();
            AssignPrivateField(controller, "levelService", levelService);
            AssignPrivateField(controller, "goldService", goldService);
            AssignPrivateField(controller, "gameEngine", engine);
            return controller;
        }

        private void StartLevel(MainMenuViewController controller)
        {
            FakeNavigation navigation = new FakeNavigation();
            controller.Bind(navigation);
            controller.StartLevel();
        }

        private static ILevelService BuildLevelService()
        {
            LevelId firstId = new LevelId("L1");
            LevelId secondId = new LevelId("L2");
            Level first = new Level(firstId);
            Level second = new Level(secondId);
            Level[] levels = { first, second };
            LevelCatalog catalog = new LevelCatalog(levels);
            LevelProgression progression = new LevelProgression(0);
            return new LevelService(progression, catalog);
        }

        private static IGoldService BuildGoldService()
        {
            GoldWallet wallet = new GoldWallet(100);
            GoldConfig config = new GoldConfig(0, 999999);
            return new GoldService(wallet, config);
        }

        private void AssignPrivateField(object target, string fieldName, object value)
        {
            System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            System.Reflection.FieldInfo field = target.GetType().GetField(fieldName, flags);
            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }

        private void AssertStartedState(MainMenuViewController controller)
        {
            Assert.AreEqual("State: started", controller.LevelStateLabel);
            Assert.IsFalse(controller.CanStartLevel);
            Assert.IsTrue(controller.CanFinishLevel);
        }

        private void AssertDoneState(MainMenuViewController controller)
        {
            Assert.AreEqual("State: done", controller.LevelStateLabel);
            Assert.IsTrue(controller.CanStartLevel);
            Assert.IsFalse(controller.CanFinishLevel);
        }

        private sealed class RecordingGameEngine : IGameEngine
        {
            public string LastCreatedLevelId { get; private set; }
            public Game LastCreatedGame { get; private set; }

            public Game CreateGame(Level level)
            {
                LastCreatedLevelId = level.Id.Value;
                LastCreatedGame = new Game(level);
                return LastCreatedGame;
            }
        }

        private sealed class FakeNavigation : INavigation
        {
            public NavigationPoint CurrentPoint => null;

            public void Open<TViewController>(TViewController controller, bool closeCurrent = false, NavigationOptions options = null) where TViewController : IViewController { }

            public void Close<TViewController>(TViewController controller) where TViewController : IViewController { }

            public IViewController Return()
            {
                return null;
            }
        }

        private sealed class ViewHarness : System.IDisposable
        {
            private readonly GameObject root;
            private readonly GameObject startButtonObject;
            private readonly GameObject finishButtonObject;

            public MainMenuView View { get; }

            public ViewHarness(GameObject root, GameObject startButtonObject, GameObject finishButtonObject, MainMenuView view)
            {
                this.root = root;
                this.startButtonObject = startButtonObject;
                this.finishButtonObject = finishButtonObject;
                View = view;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(startButtonObject);
                Object.DestroyImmediate(finishButtonObject);
            }
        }
    }
}
