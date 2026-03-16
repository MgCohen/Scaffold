using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Madbox.Meta.Level
{
    [Serializable]
    public sealed class LevelCatalog
    {
        [SerializeField] private List<Level> levels;

        public LevelCatalog(IReadOnlyList<Level> levels)
        {
            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            if (levels.Count == 0)
            {
                throw new ArgumentException("Level catalog cannot be empty.", nameof(levels));
            }

            this.levels = levels.ToList();
        }

        public IReadOnlyList<Level> Levels => levels;
    }
}
