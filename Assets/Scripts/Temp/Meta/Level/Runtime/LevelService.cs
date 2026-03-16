using System;
using System.Collections.Generic;

namespace Madbox.Meta.Level
{
    public sealed class LevelService : ILevelService
    {
        private readonly LevelProgression levelProgression;
        private readonly LevelCatalog levelCatalog;

        public LevelService(LevelProgression levelProgressionValue, LevelCatalog levelCatalogValue)
        {
            if (levelProgressionValue is null) { throw new ArgumentNullException(nameof(levelProgressionValue)); }
            if (levelCatalogValue is null) { throw new ArgumentNullException(nameof(levelCatalogValue)); }
            levelProgression = levelProgressionValue;
            levelCatalog = levelCatalogValue;
        }

        public Level GetCurrentLevel()
        {
            EnsureStateIsValid();
            int index = GetSafeLevelIndex();
            return levelCatalog.Levels[index];
        }

        public IReadOnlyList<Level> GetLevels()
        {
            EnsureStateIsValid();
            return levelCatalog.Levels;
        }

        public void AdvanceToNextLevel()
        {
            EnsureStateIsValid();
            int maxLevelIndex = levelCatalog.Levels.Count - 1;
            levelProgression.AdvanceToNextLevel(maxLevelIndex);
        }

        private int GetSafeLevelIndex()
        {
            int index = levelProgression.NextLevelIndex;
            int maxIndex = levelCatalog.Levels.Count - 1;
            return index > maxIndex ? maxIndex : index;
        }

        private void EnsureStateIsValid()
        {
            if (levelCatalog.Levels.Count == 0)
            {
                throw new InvalidOperationException("Level catalog must contain at least one level.");
            }
        }
    }
}
