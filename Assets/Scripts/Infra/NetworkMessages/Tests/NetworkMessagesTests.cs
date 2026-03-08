using NUnit.Framework;

namespace Scaffold.NetworkMessages.Tests
{
    public class NetworkMessagesTests
    {
        [Test]
        public void EquatableWrapper_SameValue_EqualsReturnsTrue()
        {
            EquatableWrapper<int> a = new EquatableWrapper<int>(42);
            EquatableWrapper<int> b = new EquatableWrapper<int>(42);
            bool result = a.Equals(b);
            Assert.IsTrue(result);
        }

        [Test]
        public void EquatableWrapper_DifferentValues_EqualsReturnsFalse()
        {
            EquatableWrapper<int> a = new EquatableWrapper<int>(1);
            EquatableWrapper<int> b = new EquatableWrapper<int>(2);
            bool result = a.Equals(b);
            Assert.IsFalse(result);
        }

        [Test]
        public void EquatableWrapper_Value_MatchesConstructorArgument()
        {
            EquatableWrapper<int> wrapper = new EquatableWrapper<int>(99);
            Assert.AreEqual(99, wrapper.Value);
        }
    }
}
