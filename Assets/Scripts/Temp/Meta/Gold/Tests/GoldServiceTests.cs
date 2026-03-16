using NUnit.Framework;
using System;

namespace Madbox.Meta.Gold.Tests
{
    public class GoldServiceTests
    {
        [Test]
        public void GetCurrentGold_ReturnsWalletValue()
        {
            IGoldService service = CreateService(currentGold: 250);
            int currentGold = service.GetCurrentGold();
            Assert.AreEqual(250, currentGold);
        }

        [Test]
        public void Constructor_WhenWalletOutsideConfig_Throws()
        {
            TestDelegate action = BuildOutOfRangeConstructorAction();
            Assert.Throws<ArgumentOutOfRangeException>(action);
        }

        private static IGoldService CreateService(int currentGold)
        {
            GoldWallet wallet = new GoldWallet(currentGold);
            GoldConfig config = new GoldConfig(minimumGold: 0, maximumGold: 500);
            return new GoldService(wallet, config);
        }

        private static TestDelegate BuildOutOfRangeConstructorAction()
        {
            GoldWallet wallet = new GoldWallet(currentGold: -1);
            GoldConfig config = new GoldConfig(minimumGold: 0, maximumGold: 500);
            return () => new GoldService(wallet, config);
        }
    }
}
