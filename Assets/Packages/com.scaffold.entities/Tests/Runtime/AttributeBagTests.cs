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

        [Test]
        public void SetLocalSilent_RemoveLocalSilent_DoNotFireStructuralEvents()
        {
            var bag = new AttributeBag();
            bag.RebuildCache();
            var key = new Attribute("Silent", AttributeValueType.Float);
            int addedEvents = 0;
            int removedEvents = 0;
            bag.OnAttributeAdded += (_, _) => addedEvents++;
            bag.OnAttributeRemoved += _ => removedEvents++;

            bag.SetLocalSilent(key, new FloatAttributeValue { Value = 9f });
            Assert.That(bag.TryGetBase(key, out AttributeValue v), Is.True);
            Assert.That(((FloatAttributeValue)v).Value, Is.EqualTo(9f));
            Assert.That(addedEvents, Is.EqualTo(0));

            Assert.That(bag.RemoveLocalSilent(key), Is.True);
            Assert.That(bag.HasLocalKey(key), Is.False);
            Assert.That(removedEvents, Is.EqualTo(0));
        }

        [Test]
        public void SetLocalSilent_NullValue_DoesNotStore()
        {
            var bag = new AttributeBag();
            bag.RebuildCache();
            var key = new Attribute("N", AttributeValueType.Float);
            bag.SetLocalSilent(key, null!);
            Assert.That(bag.HasLocalKey(key), Is.False);
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
