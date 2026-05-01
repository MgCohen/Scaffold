using System;
using NUnit.Framework;
using Scaffold.Entities;

namespace Scaffold.Entities.Tests
{
    public sealed class VariableValueFactoryTests
    {
        [Test]
        public void CreateDefault_UnregisteredType_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => VariableValueFactory.CreateDefault(typeof(VariableValue)));
        }

        [Test]
        public void CreateDefault_FloatType_ReturnsFloatVariableValue()
        {
            VariableValue v = VariableValueFactory.CreateDefault(typeof(FloatVariableValue));
            Assert.That(v, Is.TypeOf<FloatVariableValue>());
        }

        [Test]
        public void TryGetId_FloatVariableValue_ReturnsFloat()
        {
            Assert.That(VariableValueRegistry.TryGetId(typeof(FloatVariableValue), out string id), Is.True);
            Assert.That(id, Is.EqualTo("float"));
        }
    }
}
