using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.Entities;
using UnityEngine;

namespace Scaffold.Entities.Tests
{
    public sealed class AttributeBagTests
    {
        [Test]
        public void TryGetBase_SingleBag_ReturnsSerializedEntry()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            var bag = new AttributeBag();
            bag.AddSerializedEntry(AttributeEntry.Create(hp, new FloatAttributeValue { Value = 7f }));
            bag.RebuildCache();

            Assert.That(bag.TryGetBase((Attribute)hp, out AttributeValue v), Is.True);
            Assert.That(((FloatAttributeValue)v).Value, Is.EqualTo(7f));
        }

        [Test]
        public void TryGetBase_ChildFallsBackToParent()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            var parent = new AttributeBag();
            parent.AddSerializedEntry(AttributeEntry.Create(hp, new FloatAttributeValue { Value = 10f }));
            parent.RebuildCache();

            var child = new AttributeBag();
            child.SetParent(parent);
            child.RebuildCache();

            Assert.That(child.TryGetBase((Attribute)hp, out AttributeValue v), Is.True);
            Assert.That(((FloatAttributeValue)v).Value, Is.EqualTo(10f));
        }

        [Test]
        public void TryGetBase_ChildWinsOverParent()
        {
            AttributeSO hp = CreateAttributeSo("HP", AttributeValueType.Float);
            var parent = new AttributeBag();
            parent.AddSerializedEntry(AttributeEntry.Create(hp, new FloatAttributeValue { Value = 10f }));
            parent.RebuildCache();

            var child = new AttributeBag();
            child.SetParent(parent);
            child.RebuildCache();
            Assert.That(child.Add((Attribute)hp, new FloatAttributeValue { Value = 3f }), Is.True);

            Assert.That(child.TryGetBase((Attribute)hp, out AttributeValue v), Is.True);
            Assert.That(((FloatAttributeValue)v).Value, Is.EqualTo(3f));
        }

        [Test]
        public void Add_Remove_FiresStructuralEvents()
        {
            var bag = new AttributeBag();
            bag.RebuildCache();
            var key = new Attribute("Poison", AttributeValueType.Float);
            var added = new List<(Attribute, AttributeValue)>();
            var removed = new List<Attribute>();
            bag.OnAttributeAdded += (k, v) => added.Add((k, v));
            bag.OnAttributeRemoved += removed.Add;

            Assert.That(bag.Add(key, new FloatAttributeValue { Value = 2f }), Is.True);
            Assert.That(added.Count, Is.EqualTo(1));
            Assert.That(added[0].Item1, Is.EqualTo(key));
            Assert.That(((FloatAttributeValue)added[0].Item2).Value, Is.EqualTo(2f));

            Assert.That(bag.Remove(key), Is.True);
            Assert.That(removed.Count, Is.EqualTo(1));
            Assert.That(removed[0], Is.EqualTo(key));
        }

        [Test]
        public void Add_DuplicateLocal_ReturnsFalse()
        {
            var bag = new AttributeBag();
            bag.RebuildCache();
            var key = new Attribute("X", AttributeValueType.Int);
            Assert.That(bag.Add(key, new IntAttributeValue { Value = 1 }), Is.True);
            Assert.That(bag.Add(key, new IntAttributeValue { Value = 2 }), Is.False);
        }

        private static AttributeSO CreateAttributeSo(string assetName, AttributeValueType valueType)
        {
            var so = ScriptableObject.CreateInstance<AttributeSO>();
            so.name = assetName;
            so.SetValueType(valueType);
            return so;
        }
    }
}
