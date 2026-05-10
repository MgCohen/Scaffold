using NUnit.Framework;
using Unity.Properties;

namespace Scaffold.GraphFlow.PropertiesSpike.Editor
{
    // E1 — Q-PICKUP. Verifies the source generator emitted a usable bag for
    // a hand-annotated type. If any of these fail, the migration is dead and
    // the sketch needs to be reworked or shelved.
    public sealed class PickupTest
    {
        [Test]
        public void PropertyBag_Resolves_For_SpikeEvent()
        {
            var bag = PropertyBag.GetPropertyBag<SpikeEvent>();
            Assert.That(bag, Is.Not.Null,
                "PropertyBag.GetPropertyBag<SpikeEvent>() returned null. " +
                "Either com.unity.properties is not installed or the bag failed to register.");
        }

        [Test]
        public void PropertyBag_Is_Source_Generated_Not_Reflection()
        {
            var bag = PropertyBag.GetPropertyBag<SpikeEvent>();
            Assert.That(bag, Is.Not.Null);

            var typeName = bag.GetType().FullName ?? string.Empty;
            Assert.That(typeName, Does.Not.Contain("Reflected"),
                $"Bag is reflection-based ({typeName}). " +
                "Source generator did not pick up [GeneratePropertyBag] / [GeneratePropertyBagsForAssembly].");
        }

        [Test]
        public void PropertyBag_Enumerates_All_Six_Fields()
        {
            var bag = PropertyBag.GetPropertyBag<SpikeEvent>();
            Assert.That(bag, Is.Not.Null);

            int count = 0;
            foreach (var _ in bag.GetProperties())
                count++;

            Assert.That(count, Is.EqualTo(6),
                "Expected 6 properties on SpikeEvent (Health, Name, Position, Damage, Hit, InternalCounter).");
        }

        [Test]
        public void PropertyBag_Resolves_For_SpikeCommand_And_SpikeEntry()
        {
            Assert.That(PropertyBag.GetPropertyBag<SpikeCommand>(), Is.Not.Null);
            Assert.That(PropertyBag.GetPropertyBag<SpikeEntry>(), Is.Not.Null);
        }
    }
}
