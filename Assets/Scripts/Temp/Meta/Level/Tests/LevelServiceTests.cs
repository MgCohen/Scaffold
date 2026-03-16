using NUnit.Framework;

namespace Madbox.Meta.Level.Tests
{
    public class LevelServiceTests
    {
        [Test]
        public void GetCurrentLevel_WithValidProgression_ReturnsExpectedLevel()
        {
            ILevelService service = CreateService(1);
            Level next = service.GetCurrentLevel();
            AssertLevelId(next, "L2");
        }

        [Test]
        public void GetCurrentLevel_WithOutOfRangeProgression_ReturnsLastLevel()
        {
            ILevelService service = CreateService(99);
            Level next = service.GetCurrentLevel();
            AssertLevelId(next, "L2");
        }

        [Test]
        public void AdvanceToNextLevel_WhenNotAtLast_AdvancesCurrentLevel()
        {
            ILevelService service = CreateService(0);

            service.AdvanceToNextLevel();
            Level next = service.GetCurrentLevel();

            AssertLevelId(next, "L2");
        }

        [Test]
        public void AdvanceToNextLevel_WhenAtLast_StaysOnLastLevel()
        {
            ILevelService service = CreateService(1);

            service.AdvanceToNextLevel();
            Level next = service.GetCurrentLevel();

            AssertLevelId(next, "L2");
        }

        private static ILevelService CreateService(int nextLevelIndex)
        {
            LevelProgression progression = new LevelProgression(nextLevelIndex);
            LevelCatalog catalog = CreateCatalog();
            return new LevelService(progression, catalog);
        }

        private static LevelCatalog CreateCatalog()
        {
            Level first = BuildLevel("L1");
            Level second = BuildLevel("L2");
            Level[] levels = { first, second };
            return new LevelCatalog(levels);
        }

        private static Level BuildLevel(string levelIdValue)
        {
            LevelId levelId = new LevelId(levelIdValue);
            return new Level(levelId);
        }

        private void AssertLevelId(Level level, string expected)
        {
            string actual = level.Id.Value;
            Assert.AreEqual(expected, actual);
        }
    }
}

