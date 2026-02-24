using NUnit.Framework;

namespace Scaffold.AutoPacker.Tests
{
    /// <summary>
    /// Tests for the AutoPacker utility.
    /// </summary>
    public class AutoPackerTests
    {
        [Test]
        public void Pack_DoesNotThrow()
        {
            var packer = new AutoPacker();
            packer.Pack();
            Assert.Pass();
        }
    }
}
