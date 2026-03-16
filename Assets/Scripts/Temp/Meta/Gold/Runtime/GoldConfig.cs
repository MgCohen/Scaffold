using System;

namespace Madbox.Meta.Gold
{
    [Serializable]
    public sealed class GoldConfig
    {
        public GoldConfig(int minimumGold, int maximumGold)
        {
            if (maximumGold < minimumGold)
            {
                throw new ArgumentException("Maximum gold must be greater than or equal to minimum gold.");
            }

            MinimumGold = minimumGold;
            MaximumGold = maximumGold;
        }

        public int MinimumGold { get; }
        public int MaximumGold { get; }
    }
}
