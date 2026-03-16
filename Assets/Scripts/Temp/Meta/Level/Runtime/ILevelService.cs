using System.Collections.Generic;

namespace Madbox.Meta.Level
{
    public interface ILevelService
    {
        Level GetCurrentLevel();
        IReadOnlyList<Level> GetLevels();
        void AdvanceToNextLevel();
    }
}
