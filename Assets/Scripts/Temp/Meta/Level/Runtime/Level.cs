using System;
using UnityEngine;

namespace Madbox.Meta.Level
{
    [Serializable]
    public sealed class Level
    {
        [SerializeField] private LevelId id;

        public Level(LevelId idValue)
        {
            if (string.IsNullOrWhiteSpace(idValue.Value))
            {
                throw new ArgumentException("Level id cannot be empty.", nameof(idValue));
            }

            id = idValue;
        }

        public LevelId Id => id;
    }
}
