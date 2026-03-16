using System;
using UnityEngine;

namespace Madbox.Meta.Level
{
    [Serializable]
    public sealed class LevelProgression
    {
        [SerializeField] private int nextLevelIndex;

        public LevelProgression(int nextLevelIndex)
        {
            if (nextLevelIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nextLevelIndex), "Next level index cannot be negative.");
            }

            this.nextLevelIndex = nextLevelIndex;
        }

        public int NextLevelIndex => nextLevelIndex;

        public void AdvanceToNextLevel(int maxLevelIndex)
        {
            EnsureMaxLevelIndex(maxLevelIndex);
            if (nextLevelIndex >= maxLevelIndex) { return; }
            nextLevelIndex++;
        }

        private void EnsureMaxLevelIndex(int maxLevelIndex)
        {
            if (maxLevelIndex >= 0) { return; }
            throw new ArgumentOutOfRangeException(nameof(maxLevelIndex), "Max level index cannot be negative.");
        }
    }
}
