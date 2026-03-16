using System;

namespace Madbox.Meta.Gold
{
    public sealed class GoldService : IGoldService
    {
        private readonly GoldWallet goldWallet;
        private readonly GoldConfig goldConfig;

        public GoldService(GoldWallet goldWallet, GoldConfig goldConfig)
        {
            if (goldWallet is null) { throw new ArgumentNullException(nameof(goldWallet)); }
            if (goldConfig is null) { throw new ArgumentNullException(nameof(goldConfig)); }
            this.goldWallet = goldWallet;
            this.goldConfig = goldConfig;
            ValidateWalletRange();
        }

        public int GetCurrentGold()
        {
            EnsureStateIsValid();
            return goldWallet.CurrentGold;
        }

        private void EnsureStateIsValid()
        {
            ValidateWalletRange();
        }

        private void ValidateWalletRange()
        {
            int current = goldWallet.CurrentGold;
            if (current < goldConfig.MinimumGold || current > goldConfig.MaximumGold)
            {
                throw new ArgumentOutOfRangeException(nameof(goldWallet), "Current gold must be within configured bounds.");
            }
        }
    }
}
