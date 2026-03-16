using System;

namespace Madbox.Meta.Gold
{
    [Serializable]
    public sealed class GoldWallet
    {
        public GoldWallet(int currentGold)
        {
            ValidateCurrentGold(currentGold);
            CurrentGold = currentGold;
        }

        public int CurrentGold { get; }

        private void ValidateCurrentGold(int currentGold)
        {
            _ = currentGold;
        }
    }
}
