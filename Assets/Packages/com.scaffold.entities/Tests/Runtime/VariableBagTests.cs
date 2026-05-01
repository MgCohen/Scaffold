using System;
using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.Entities;
using UnityEngine;

namespace Scaffold.Entities.Tests
{
    public sealed class VariableBagTests
    {
        [Test]
        public void TryGetBase_SingleBag_ReturnsSerializedEntry()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            var bag = new VariableBag();
            bag.AddSerializedEntry(VariableEntry.Create((Variable)hp, new FloatVariableValue { Value = 7f }));
            bag.RebuildCache();

            Assert.That(bag.TryGetBase((Variable)hp, out VariableValue v), Is.True);
            Assert.That(((FloatVariableValue)v).Value, Is.EqualTo(7f));
        }

        [Test]
        public void TryGetBase_ChildFallsBackToParent()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            var parent = new VariableBag();
            parent.AddSerializedEntry(VariableEntry.Create((Variable)hp, new FloatVariableValue { Value = 10f }));
            parent.RebuildCache();

            var child = new VariableBag();
            child.SetParent(parent);
            child.RebuildCache();

            Assert.That(child.TryGetBase((Variable)hp, out VariableValue v), Is.True);
            Assert.That(((FloatVariableValue)v).Value, Is.EqualTo(10f));
        }

        [Test]
        public void TryGetBase_ChildWinsOverParent()
        {
            VariableSO hp = CreateVariableSo("HP", typeof(FloatVariableValue));
            var parent = new VariableBag();
            parent.AddSerializedEntry(VariableEntry.Create((Variable)hp, new FloatVariableValue { Value = 10f }));
            parent.RebuildCache();

            var child = new VariableBag();
            child.SetParent(parent);
            child.RebuildCache();
            Assert.That(child.Add((Variable)hp, new FloatVariableValue { Value = 3f }), Is.True);

            Assert.That(child.TryGetBase((Variable)hp, out VariableValue v), Is.True);
            Assert.That(((FloatVariableValue)v).Value, Is.EqualTo(3f));
        }

        [Test]
        public void Add_Remove_FiresStructuralEvents()
        {
            var bag = new VariableBag();
            bag.RebuildCache();
            var key = new Variable("Poison", "float");
            var added = new List<(Variable, VariableValue)>();
            var removed = new List<Variable>();
            bag.OnVariableStructuralChange += (kind, k, v) =>
            {
                if (kind == VariableStructuralChange.Added && v != null)
                {
                    added.Add((k, v));
                }
                else if (kind == VariableStructuralChange.Removed)
                {
                    removed.Add(k);
                }
            };

            Assert.That(bag.Add(key, new FloatVariableValue { Value = 2f }), Is.True);
            Assert.That(added.Count, Is.EqualTo(1));
            Assert.That(added[0].Item1, Is.EqualTo(key));
            Assert.That(((FloatVariableValue)added[0].Item2).Value, Is.EqualTo(2f));

            Assert.That(bag.Remove(key), Is.True);
            Assert.That(removed.Count, Is.EqualTo(1));
            Assert.That(removed[0], Is.EqualTo(key));
        }

        [Test]
        public void Add_DuplicateLocal_ReturnsFalse()
        {
            var bag = new VariableBag();
            bag.RebuildCache();
            var key = new Variable("X", "int");
            Assert.That(bag.Add(key, new IntVariableValue { Value = 1 }), Is.True);
            Assert.That(bag.Add(key, new IntVariableValue { Value = 2 }), Is.False);
        }

        [Test]
        public void SetLocalSilent_RemoveLocalSilent_DoNotFireStructuralEvents()
        {
            var bag = new VariableBag();
            bag.RebuildCache();
            var key = new Variable("Silent", "float");
            int addedEvents = 0;
            int removedEvents = 0;
            bag.OnVariableStructuralChange += (kind, _, _) =>
            {
                if (kind == VariableStructuralChange.Added)
                {
                    addedEvents++;
                }
                else
                {
                    removedEvents++;
                }
            };

            bag.SetLocalSilent(key, new FloatVariableValue { Value = 9f });
            Assert.That(bag.TryGetBase(key, out VariableValue v), Is.True);
            Assert.That(((FloatVariableValue)v).Value, Is.EqualTo(9f));
            Assert.That(addedEvents, Is.EqualTo(0));

            Assert.That(bag.RemoveLocalSilent(key), Is.True);
            Assert.That(bag.HasLocalKey(key), Is.False);
            Assert.That(removedEvents, Is.EqualTo(0));
        }

        [Test]
        public void SetLocalSilent_NullValue_DoesNotStore()
        {
            var bag = new VariableBag();
            bag.RebuildCache();
            var key = new Variable("N", "float");
            bag.SetLocalSilent(key, null!);
            Assert.That(bag.HasLocalKey(key), Is.False);
        }

        private static VariableSO CreateVariableSo(string assetName, Type payloadType)
        {
            var so = ScriptableObject.CreateInstance<VariableSO>();
            so.name = assetName;
            so.SetPayloadType(payloadType);
            return so;
        }
    }
}
