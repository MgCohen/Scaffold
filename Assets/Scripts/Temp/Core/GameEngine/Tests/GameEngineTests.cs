using NUnit.Framework;
using Madbox.Meta.Level;
using System.Collections.Generic;

namespace Madbox.GameEngine.Tests
{
    public class GameEngineTests
    {
        [Test]
        public void CreateGame_Start_SetsStartedState_WithoutCompletion()
        {
            Game game = CreateGame();
            CompletionProbe probe = BuildCompletionProbe(game);
            PropertyProbe propertyProbe = BuildPropertyProbe(game);
            StartGame(game);
            AssertStartedWithoutCompletion(game, probe, propertyProbe);
        }

        [Test]
        public void Finish_WhenStarted_SetsFinishedAndRaisesCompletedEvent()
        {
            Game game = CreateGame();
            CompletionProbe probe = BuildCompletionProbe(game);
            PropertyProbe propertyProbe = BuildPropertyProbe(game);
            StartAndFinish(game);
            AssertFinishedWithSuccess(game, probe, propertyProbe);
        }

        [Test]
        public void Finish_CalledMultipleTimes_RaisesCompletedOnlyOnce()
        {
            Game game = CreateGame();
            int callCount = 0;
            game.Completed += _ => callCount++;
            StartAndFinishTwice(game);
            Assert.AreEqual(GameState.Finished, game.State);
            Assert.AreEqual(1, callCount);
        }

        private Game CreateGame()
        {
            Level level = BuildLevel("L1");
            IGameEngine engine = new GameEngine();
            return engine.CreateGame(level);
        }

        private Level BuildLevel(string idValue)
        {
            LevelId levelId = new LevelId(idValue);
            return new Level(levelId);
        }

        private CompletionProbe BuildCompletionProbe(Game game)
        {
            CompletionProbe probe = new CompletionProbe();
            game.Completed += probe.MarkCompleted;
            return probe;
        }

        private PropertyProbe BuildPropertyProbe(Game game)
        {
            PropertyProbe probe = new PropertyProbe();
            game.PropertyChanged += probe.MarkChanged;
            return probe;
        }

        private void StartGame(Game game)
        {
            game.Start();
        }

        private void StartAndFinish(Game game)
        {
            game.Start();
            game.Finish();
        }

        private void StartAndFinishTwice(Game game)
        {
            game.Start();
            game.Finish();
            game.Finish();
        }

        private void AssertFinishedWithSuccess(Game game, CompletionProbe probe, PropertyProbe propertyProbe)
        {
            Assert.AreEqual(GameState.Finished, game.State);
            Assert.IsTrue(probe.CompletedRaised);
            Assert.IsTrue(probe.Result);
            CollectionAssert.AreEqual(new[] { nameof(Game.State), nameof(Game.State) }, propertyProbe.ChangedProperties);
        }

        private void AssertStartedWithoutCompletion(Game game, CompletionProbe probe, PropertyProbe propertyProbe)
        {
            Assert.AreEqual(GameState.Started, game.State);
            Assert.IsFalse(probe.CompletedRaised);
            CollectionAssert.AreEqual(new[] { nameof(Game.State) }, propertyProbe.ChangedProperties);
        }

        private sealed class CompletionProbe
        {
            public bool CompletedRaised { get; private set; }
            public bool Result { get; private set; }

            public void MarkCompleted(bool result)
            {
                CompletedRaised = true;
                Result = result;
            }
        }

        private sealed class PropertyProbe
        {
            public List<string> ChangedProperties { get; } = new List<string>();

            public void MarkChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                ChangedProperties.Add(e.PropertyName);
            }
        }
    }
}
