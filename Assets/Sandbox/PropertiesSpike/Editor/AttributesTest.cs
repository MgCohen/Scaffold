using System.Collections.Generic;
using NUnit.Framework;
using Unity.Properties;

namespace Scaffold.GraphFlow.PropertiesSpike.Editor
{
    // E2 — Q-ATTRS. Verifies user attributes attached to fields survive onto
    // the source-generated IProperty so the runtime port binder can read them
    // via HasAttribute<T>() / GetAttribute<T>(). If these fail, the visitor
    // approach can't classify port direction without falling back to
    // reflection on FieldInfo, which defeats the migration's perf goal.
    public sealed class AttributesTest
    {
        private static Dictionary<string, IProperty<SpikeEvent>> Index()
        {
            var bag = PropertyBag.GetPropertyBag<SpikeEvent>();
            Assert.That(bag, Is.Not.Null, "Bag missing — run PickupTest first to diagnose.");

            var byName = new Dictionary<string, IProperty<SpikeEvent>>();
            foreach (var p in bag.GetProperties())
                byName[p.Name] = p;
            return byName;
        }

        [Test]
        public void In_Attribute_Survives_On_Generated_Property()
        {
            var idx = Index();
            Assert.That(idx[nameof(SpikeEvent.Health)].HasAttribute<InAttribute>(), Is.True);
            Assert.That(idx[nameof(SpikeEvent.Name)].HasAttribute<InAttribute>(), Is.True);
        }

        [Test]
        public void Out_Attribute_Survives_On_Generated_Property()
        {
            var idx = Index();
            Assert.That(idx[nameof(SpikeEvent.Position)].HasAttribute<OutAttribute>(), Is.True);
        }

        [Test]
        public void GraphPort_Attribute_Survives_On_Generated_Property()
        {
            var idx = Index();
            Assert.That(idx[nameof(SpikeEvent.Damage)].HasAttribute<GraphPortAttribute>(), Is.True);
        }

        [Test]
        public void GraphPortIgnore_Attribute_Survives_On_Generated_Property()
        {
            var idx = Index();
            Assert.That(idx[nameof(SpikeEvent.Hit)].HasAttribute<GraphPortIgnoreAttribute>(), Is.True);
        }

        [Test]
        public void Untagged_Field_Has_No_Port_Attributes()
        {
            var idx = Index();
            var ic = idx[nameof(SpikeEvent.InternalCounter)];
            Assert.That(ic.HasAttribute<InAttribute>(), Is.False);
            Assert.That(ic.HasAttribute<OutAttribute>(), Is.False);
            Assert.That(ic.HasAttribute<GraphPortAttribute>(), Is.False);
            Assert.That(ic.HasAttribute<GraphPortIgnoreAttribute>(), Is.False);
        }

        [Test]
        public void In_Attribute_Not_Visible_On_Out_Tagged_Field()
        {
            var idx = Index();
            Assert.That(idx[nameof(SpikeEvent.Position)].HasAttribute<InAttribute>(), Is.False,
                "Position is [Out] only — HasAttribute<InAttribute>() must return false.");
        }
    }
}
