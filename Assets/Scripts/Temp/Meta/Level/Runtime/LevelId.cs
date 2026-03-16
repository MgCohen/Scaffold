using System;

namespace Madbox.Meta.Level
{
    [Serializable]
    public readonly struct LevelId
    {
        public LevelId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Level id cannot be empty.", nameof(value));
            }
            Value = value;
        }

        public string Value { get; }

        public override string ToString()
        {
            return Value;
        }
    }
}
