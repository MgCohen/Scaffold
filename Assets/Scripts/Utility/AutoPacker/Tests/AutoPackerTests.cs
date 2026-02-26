using NUnit.Framework;
using Scaffold.AutoPacker;

namespace Scaffold.AutoPacker.Tests
{
    [AutoPack]
    public partial class SamplePackable
    {
        [Packed]
        public int Value;

        [Packed(typeof(float))]
        public int ConvertedValue;

        public SamplePackable() { }

        public SamplePackable(int value, int convertedValue)
        {
            Value = value;
            ConvertedValue = convertedValue;
        }
    }

    public class MockPackingHandler : IPackingHandler
    {
        public TTarget Resolve<TSource, TTarget>(TSource source)
        {
            if (source is int intSource && typeof(TTarget) == typeof(int))
            {
                return (TTarget)(object)(intSource + 1);
            }
            return (TTarget)System.Convert.ChangeType(source, typeof(TTarget));
        }
    }

    public class AutoPackerTests
    {
        [Test]
        public void Pack_CreatesPackedStructWithCorrectValues()
        {
            var original = new SamplePackable(10, 20);
            var packed = (SamplePackable.Packed)original.Pack();

            Assert.AreEqual(10, packed.Value);
            Assert.AreEqual(20.0f, packed.ConvertedValue);
        }

        [Test]
        public void Unpack_RestoresOriginalValues()
        {
            var original = new SamplePackable(10, 20);
            var packed = (SamplePackable.Packed)original.Pack();
            var restored = new SamplePackable(packed);

            Assert.AreEqual(original.Value, restored.Value);
            Assert.AreEqual(original.ConvertedValue, restored.ConvertedValue);
        }

        [Test]
        public void Pack_WithCustomHandler_AppliesTransformation()
        {
            var original = new SamplePackable(10, 20);
            var handler = new MockPackingHandler();
            var packed = (SamplePackable.Packed)original.Pack(handler);

            // MockPackingHandler adds 1 to int values
            Assert.AreEqual(11, packed.Value);
        }

        [Test]
        public void Unpack_WithCustomHandler_AppliesTransformation()
        {
            var original = new SamplePackable(10, 20);
            var packed = (SamplePackable.Packed)original.Pack(); // Normal pack (Value = 10)
            
            var handler = new MockPackingHandler();
            var restored = new SamplePackable(packed, handler);

            // MockPackingHandler adds 1 to int values during resolve
            Assert.AreEqual(11, restored.Value);
        }
    }
}
