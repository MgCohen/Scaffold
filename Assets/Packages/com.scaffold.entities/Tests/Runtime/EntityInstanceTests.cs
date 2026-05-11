using NUnit.Framework;
using Scaffold.Entities;
using Variable = Scaffold.Variables.Variable;

namespace Scaffold.Entities.Tests
{
    public sealed class EntityInstanceTests
    {
        private static Variable Var(string name) => new Variable(name, "int");
        private static IntVariableValue Int(int v) => new IntVariableValue(v);

        private static EntityDefinition DefWithDefault(Variable key, int value)
        {
            var def = new EntityDefinition();
            def.AddVariable(key, Int(value));
            return def;
        }

        [Test]
        public void Read_AnchorsOnStorageBase_NoModifiers()
        {
            var hp = Var("hp");
            var def = DefWithDefault(hp, 10);
            var entity = Entities.Local(def);
            entity.SetBaseValue(hp, Int(7));

            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(7));
        }

        [Test]
        public void Read_FallsBackToDefinitionDefault_WhenNoStorageBase()
        {
            var hp = Var("hp");
            var def = DefWithDefault(hp, 10);
            var entity = Entities.Local(def);

            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(10));
        }

        [Test]
        public void Read_ReturnsFalse_WhenNoAnchor()
        {
            var hp = Var("hp");
            var def = new EntityDefinition();
            var entity = Entities.Local(def);

            Assert.That(entity.TryGetVariable<int>(hp, out _), Is.False);
        }

        [Test]
        public void Read_AppliesModifierToStorageBase()
        {
            var hp = Var("hp");
            var def = DefWithDefault(hp, 10);
            var entity = Entities.Local(def);
            entity.SetBaseValue(hp, Int(5));
            entity.AddModifier(hp, new IntAddModifier(3));

            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(8));
        }

        [Test]
        public void Read_AppliesModifierToDefinitionDefault()
        {
            var hp = Var("hp");
            var def = DefWithDefault(hp, 10);
            var entity = Entities.Local(def);
            entity.AddModifier(hp, new IntAddModifier(2));

            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(12));
        }

        [Test]
        public void RemoveModifier_RestoresAnchor()
        {
            var hp = Var("hp");
            var def = DefWithDefault(hp, 10);
            var entity = Entities.Local(def);
            ModifierId id = entity.AddModifier(hp, new IntAddModifier(5));

            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(15));
            Assert.That(entity.RemoveModifier(hp, id), Is.True);
            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(10));
        }

        [Test]
        public void ClearModifiers_RestoresAnchor()
        {
            var hp = Var("hp");
            var def = DefWithDefault(hp, 10);
            var entity = Entities.Local(def);
            entity.AddModifier(hp, new IntAddModifier(5));
            entity.AddModifier(hp, new IntAddModifier(3));

            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(18));
            entity.ClearModifiers();
            Assert.That(entity.GetVariable<int>(hp), Is.EqualTo(10));
        }

        [Test]
        public void Overlay_TryGetBase_FallsBackToParent()
        {
            var hp = Var("hp");
            var baseStorage = new LocalVariableStorage();
            baseStorage.AddVariable(hp, Int(10));

            var overlay = new LocalVariableStorage(baseStorage);

            Assert.That(overlay.TryGetBase(hp, out var v), Is.True);
            Assert.That(((IntVariableValue)v).Value, Is.EqualTo(10));
        }

        [Test]
        public void Overlay_OwnBaseWinsOverParent()
        {
            var hp = Var("hp");
            var baseStorage = new LocalVariableStorage();
            baseStorage.AddVariable(hp, Int(10));

            var overlay = new LocalVariableStorage(baseStorage);
            overlay.AddVariable(hp, Int(99));

            Assert.That(overlay.TryGetBase(hp, out var v), Is.True);
            Assert.That(((IntVariableValue)v).Value, Is.EqualTo(99));
        }

        [Test]
        public void Overlay_GetModifiers_ConcatsAcrossChain()
        {
            var hp = Var("hp");
            var baseStorage = new LocalVariableStorage();
            baseStorage.AddModifier(hp, new IntAddModifier(1));

            var overlay = new LocalVariableStorage(baseStorage);
            overlay.AddModifier(hp, new IntAddModifier(2));

            int count = 0;
            foreach (var _ in overlay.GetModifiers(hp))
            {
                count++;
            }
            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void Overlay_Writes_DoNotPropagate()
        {
            var hp = Var("hp");
            var baseStorage = new LocalVariableStorage();
            baseStorage.AddVariable(hp, Int(10));

            var overlay = new LocalVariableStorage(baseStorage);
            overlay.SetBaseValue(hp, Int(99));

            Assert.That(baseStorage.TryGetBase(hp, out var bv), Is.True);
            Assert.That(((IntVariableValue)bv).Value, Is.EqualTo(10));
        }

        [Test]
        public void Variables_UnionsStorageAndDefinition()
        {
            var a = Var("a");
            var b = Var("b");
            var def = DefWithDefault(a, 1);
            var entity = Entities.Local(def);
            entity.AddVariable(b, Int(2));

            var vars = new System.Collections.Generic.HashSet<Variable>(entity.Variables);
            Assert.That(vars.Contains(a), Is.True);
            Assert.That(vars.Contains(b), Is.True);
        }
    }
}
