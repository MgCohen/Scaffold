using System;
using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.Entities;
using UnityEngine;

namespace Scaffold.Entities.Tests
{
    public sealed class AttributeValueRegistryTests
    {
        [TearDown]
        public void TearDown()
        {
            AttributeValueRegistry.Unregister(typeof(TestCustomAttributeValue));
        }

        [Test]
        public void Builtin_Float_IsRegisteredAndCreatesDefaults()
        {
            Assert.That(
                AttributeValueRegistry.TryCreate(typeof(FloatAttributeValue), out AttributeValue v),
                Is.True);
            Assert.That(v, Is.TypeOf<FloatAttributeValue>());
        }

        [Test]
        public void Register_FactoryUsedByTryCreate()
        {
            AttributeValueRegistry.Register(
                typeof(TestCustomAttributeValue),
                () => new TestCustomAttributeValue { Payload = 0L });

            Assert.That(AttributeValueRegistry.TryCreate(typeof(TestCustomAttributeValue), out AttributeValue v), Is.True);
            Assert.That(v, Is.TypeOf<TestCustomAttributeValue>());
            Assert.That(((TestCustomAttributeValue)v).Payload, Is.EqualTo(0L));
        }

        [Test]
        public void RegisterRange_FromIocEnumerable_RegistersAll()
        {
            AttributeValueRegistry.RegisterRange(
                new (Type, Func<AttributeValue>)[]
                {
                    (typeof(TestCustomAttributeValue), () => new TestCustomAttributeValue { Payload = 0L })
                });

            Assert.That(AttributeValueRegistry.TryCreate(typeof(TestCustomAttributeValue), out _), Is.True);
        }

        [Test]
        public void AttributeEntry_CustomType_ResolvesFromRegistry()
        {
            AttributeValueRegistry.Register(
                typeof(TestCustomAttributeValue),
                () => new TestCustomAttributeValue { Payload = 0L });
            AttributeSO so = CreateCustomAttributeSo("CustomStat", typeof(TestCustomAttributeValue));
            AttributeEntry entry = AttributeEntry.Create(so, null);
            entry.EnsureValueMatchesType();

            Assert.That(entry.BaseValue, Is.TypeOf<TestCustomAttributeValue>());
        }

        [Test]
        public void Attribute_Equality_IncludesCustomTypeName()
        {
            var a = new Attribute("K", AttributeValueType.Custom, typeof(TestCustomAttributeValue).AssemblyQualifiedName);
            var b = new Attribute("K", AttributeValueType.Custom, typeof(TestCustomAttributeValue).AssemblyQualifiedName);
            var c = new Attribute("K", AttributeValueType.Float);

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a, Is.Not.EqualTo(c));
        }

        private static AttributeSO CreateCustomAttributeSo(string name, Type concrete)
        {
            var so = ScriptableObject.CreateInstance<AttributeSO>();
            so.name = name;
            so.SetValueType(AttributeValueType.Custom);
            so.SetCustomValueTypeName(concrete.AssemblyQualifiedName!);
            return so;
        }

        [Serializable]
        private sealed class TestCustomAttributeValue : AttributeValue, IAttributeValue<long>
        {
            public override AttributeValueType Type => AttributeValueType.Custom;

            public long Payload;

            public long Get() => Payload;

            public override AttributeValue Combine(IReadOnlyList<AttributeValue> contributions)
            {
                long sum = Payload;
                for (int i = 0; i < contributions.Count; i++)
                {
                    if (contributions[i] is TestCustomAttributeValue c)
                    {
                        sum += c.Payload;
                    }
                }

                return new TestCustomAttributeValue { Payload = sum };
            }
        }
    }
}
