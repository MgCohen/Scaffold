using System;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Scaffold.AutoPacker;

namespace Scaffold.Autopacker.Tests
{
    // The record id shape: one abstract base with the key, one line for each typed id.
    [AutoPack]
    public abstract partial record NestedId
    {
        [Packed] public int Value { get; init; } = -1;
    }

    public sealed record NestedEntityId : NestedId;
    public sealed record NestedCardId : NestedId;

    // A partial derived record with its own member: it is its own owner, with two members.
    public sealed partial record NestedTaggedId : NestedId
    {
        [Packed] public int Tag { get; init; }
    }

    [AutoPack]
    public partial class NestedCardPlayedEvent
    {
        [Packed] public NestedEntityId Card;
        [Packed] public NestedCardId Definition;
        [Packed] public int Slot;
    }

    [AutoPack]
    public partial class NestedRange
    {
        [Packed] public int Min;
        [Packed] public int Max { get; set; }
    }

    [AutoPack]
    public partial class NestedHolder
    {
        [Packed] public NestedRange Range;
        [Packed] public NestedEntityId Owner;
    }

    [AutoPack]
    public partial class NestedOuter
    {
        [Packed] public NestedHolder Holder;
        [Packed] public NestedTaggedId Tagged;
    }

    public class NestedRecordTests
    {
        [Test]
        public void NestedRecord_PacksValueInline()
        {
            var ev = new NestedCardPlayedEvent { Card = new NestedEntityId { Value = 7 }, Slot = 3 };

            var packed = ev.PackTyped();

            Assert.AreEqual(7, packed.Card.Value);
            Assert.AreEqual(typeof(NestedId.Packed), typeof(NestedCardPlayedEvent.Packed).GetField("Card").FieldType);
            Assert.AreEqual(12, Marshal.SizeOf<NestedCardPlayedEvent.Packed>(), "Two ids and one int: 4 bytes each.");
        }

        [Test]
        public void NestedRecord_UnpacksEqualRecord()
        {
            var ev = new NestedCardPlayedEvent { Card = new NestedEntityId { Value = 7 }, Definition = new NestedCardId { Value = 9 } };

            var back = new NestedCardPlayedEvent(ev.PackTyped());

            Assert.AreEqual(new NestedEntityId { Value = 7 }, back.Card);
            Assert.AreEqual(new NestedCardId { Value = 9 }, back.Definition);
            Assert.AreNotSame(ev.Card, back.Card);
        }

        [Test]
        public void NestedRecord_NullField_PacksMinusOne()
        {
            var packed = new NestedCardPlayedEvent { Card = null }.PackTyped();

            Assert.AreEqual(-1, packed.Card.Value);
        }

        [Test]
        public void NestedRecord_MinusOne_UnpacksNull()
        {
            var packed = new NestedCardPlayedEvent.Packed { Card = new NestedId.Packed { Value = -1 } , Definition = new NestedId.Packed { Value = -1 } };

            var back = new NestedCardPlayedEvent(packed);

            Assert.IsNull(back.Card);
            Assert.IsNull(back.Definition);
        }

        [Test]
        public void NestedRecord_NoneRecord_UnpacksNull()
        {
            // A new id has Value -1 (None). None and null are the same "no id" on the wire.
            var back = new NestedCardPlayedEvent(new NestedCardPlayedEvent { Card = new NestedEntityId() }.PackTyped());

            Assert.IsNull(back.Card);
        }

        [Test]
        public void NestedRecord_ZeroKey_UnpacksRecordWithZero()
        {
            // 0 means "never set" (a bug). It stays visible: it does not become null.
            var back = new NestedCardPlayedEvent(new NestedCardPlayedEvent { Card = new NestedEntityId { Value = 0 } }.PackTyped());

            Assert.IsNotNull(back.Card);
            Assert.AreEqual(0, back.Card.Value);
        }

        [Test]
        public void NestedRecord_InPlaceUnpackOfContainer_SetsRecordField()
        {
            var packed = new NestedCardPlayedEvent { Card = new NestedEntityId { Value = 4 } }.PackTyped();
            var target = new NestedCardPlayedEvent();

            target.UnpackTyped(packed);

            Assert.AreEqual(new NestedEntityId { Value = 4 }, target.Card);
        }

        [Test]
        public void NestedRecord_NonPartialDerivedRecord_GetsNoCodegen()
        {
            Assert.IsNull(typeof(NestedEntityId).GetNestedType("Packed", BindingFlags.Public | BindingFlags.DeclaredOnly));
            foreach (var entry in AutoPackerRegistry.GetPackableTypes())
                Assert.IsFalse(entry.ContainsKey(typeof(NestedEntityId)));
        }

        [Test]
        public void NestedRecord_InitOnlyRecord_InPlaceUnpackThrows()
        {
            var packed = new NestedEntityId { Value = 1 }.Pack();

            Assert.Throws<NotSupportedException>(() => new NestedEntityId().Unpack(packed));
        }

        [Test]
        public void NestedRecord_InitOnlyRecord_ConstructorUnpacks()
        {
            var packed = new NestedTaggedId { Value = 2, Tag = 5 }.PackTyped();

            Assert.AreEqual(new NestedTaggedId { Value = 2, Tag = 5 }, new NestedTaggedId(packed));
        }

        [Test]
        public void NestedRecord_DeepNesting_RoundTrips()
        {
            var outer = new NestedOuter
            {
                Holder = new NestedHolder { Range = new NestedRange { Min = 1, Max = 9 }, Owner = new NestedEntityId { Value = 4 } },
                Tagged = new NestedTaggedId { Value = 2, Tag = 5 },
            };

            var back = new NestedOuter(outer.PackTyped());

            Assert.AreEqual(1, back.Holder.Range.Min);
            Assert.AreEqual(9, back.Holder.Range.Max);
            Assert.AreEqual(new NestedEntityId { Value = 4 }, back.Holder.Owner);
            Assert.AreEqual(new NestedTaggedId { Value = 2, Tag = 5 }, back.Tagged);
        }

        [Test]
        public void NestedRecord_NullClassWithoutKey_UnpacksDefaultObject()
        {
            // NestedHolder has two members, so there is no -1 rule: null packs as a new default object.
            var back = new NestedOuter(new NestedOuter().PackTyped());

            Assert.IsNotNull(back.Holder);
            Assert.IsNull(back.Holder.Owner, "The id inside the default object keeps -1, so it unpacks as null.");
        }
    }
}
