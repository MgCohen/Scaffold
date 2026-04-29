using System;
using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.Entities;

namespace Scaffold.Entities.Tests
{
    public sealed class ModifierTypeIndexTests
    {
        [Test]
        public void ModifiersFor_Float_IncludesFloatAddAndMultiply()
        {
            IReadOnlyList<Type> types = ModifierTypeIndex.ModifiersFor(typeof(float));
            Assert.That(ContainsType(types, typeof(FloatAddModifier)), Is.True);
            Assert.That(ContainsType(types, typeof(FloatMultiplyModifier)), Is.True);
        }

        private static bool ContainsType(IReadOnlyList<Type> types, Type expected)
        {
            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] == expected)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void TryGetValueType_FloatAdd_ReturnsFloat()
        {
            Assert.That(ModifierTypeIndex.TryGetValueType(typeof(FloatAddModifier), out Type valueType), Is.True);
            Assert.That(valueType, Is.EqualTo(typeof(float)));
        }

        [Test]
        public void AllModifierTypes_IsNonEmpty()
        {
            Assert.That(ModifierTypeIndex.AllModifierTypes.Count, Is.GreaterThanOrEqualTo(6));
        }
    }
}
