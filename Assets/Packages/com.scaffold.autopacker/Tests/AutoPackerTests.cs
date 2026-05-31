using NUnit.Framework;
using UnityEngine;
using Scaffold.AutoPacker;
using System;

namespace Scaffold.Autopacker.Tests
{
    [AutoPack]
    public partial class PlayerState
    {
        [Packed] public int Health;
        [Packed] public float Speed;
        [Packed] public Vector3 SpawnPoint;
        
        public string TransientDescription;
    }

    [AutoPack]
    public partial class SecurePayload
    {
        [Packed(typeof(int))] public string Value;
    }
    
    [AutoPack]
    public partial class ExtendedPayload
    {
        [Packed(typeof(int))] public Vector2 Coordinate;
    }

    public class MockEncryptionPacker : IPackingHandler
    {
        public const int ENCRYPTED_OFFSET = 1337;

        public TTarget Resolve<TSource, TTarget>(TSource source)
        {
            if (source == null) return default;
            
            // On Pack: String -> Int
            if (source is string text && typeof(TTarget) == typeof(int))
            {
                return (TTarget)(object)(text.GetHashCode() + ENCRYPTED_OFFSET);
            }
            // On Unpack: Int -> String
            if (source is int hash && typeof(TTarget) == typeof(string))
            {
                if (hash == "SecretMessage".GetHashCode() + ENCRYPTED_OFFSET)
                    return (TTarget)(object)"SecretMessage";
                return (TTarget)(object)"Decoded";
            }

            if (source is TTarget target) return target;
            return (TTarget)Convert.ChangeType(source, typeof(TTarget));
        }
    }
    
    public static class TestPackingExtensions
    {
        public static bool PackWasCalled = false;
        public static bool UnpackWasCalled = false;

        public const int VECTOR_HASH_OFFSET = 999;
    
        public static int Resolve(this IPackingHandler handler, Vector2 source)
        {
            PackWasCalled = true;
            return (int)(source.x + source.y) + VECTOR_HASH_OFFSET;
        }
        
        public static Vector2 Resolve(this IPackingHandler handler, int source)
        {
            UnpackWasCalled = true;
            // For testing purposes, we loosely reconstruct it back into a coordinate. 
            float val = (source - VECTOR_HASH_OFFSET) / 2f;
            return new Vector2(val, val);
        }
    }

    // --- Inheritance test types ---

    [AutoPack]
    public abstract partial class WireEvent
    {
        public string Trace;
    }

    public partial class EntityCreatedEvent : WireEvent
    {
        [Packed] public long EntityId;
    }

    [AutoPack]
    public partial class BaseStamped
    {
        [Packed] public long Timestamp;
    }

    public partial class DerivedStamped : BaseStamped
    {
        [Packed] public int Sequence;
    }

    // Proposal-C pattern: user declares the abstract base manually with interfaces + abstract Pack/Unpack.
    // Generator should yield to the user and only emit `override` on the derived.
    [AutoPack]
    public abstract class WireCommand : IPackable, IUnpackable
    {
        public abstract IPackedStruct Pack(IPackingHandler handler = null);
        public abstract void Unpack(IPackedStruct packed, IPackingHandler handler = null);
    }

    public partial class MoveCommand : WireCommand
    {
        [Packed] public int Dx;
        [Packed] public int Dy;
    }

    // Mirrors the consumer's "Config 1" — abstract base lists interfaces explicitly but
    // no abstract methods; derived has [Packed] field with NO [AutoPack] on it.
    [AutoPack]
    public abstract partial class CfgOneBase : IPackable, IUnpackable { }

    public partial class CfgOneDerived : CfgOneBase
    {
        [Packed] public long EntityId;
    }

    // Mirrors the consumer's "Config 2" — both base and derived carry [AutoPack];
    // base has explicit interfaces, derived has [Packed] field.
    [AutoPack]
    public abstract partial class CfgTwoBase : IPackable, IUnpackable { }

    [AutoPack]
    public partial class CfgTwoDerived : CfgTwoBase
    {
        [Packed] public long EntityId;
    }

    // --- Open-generic [AutoPack] types ---
    // Closed instantiations TagSetEvent<int> and TagSetEvent<float> are discovered
    // by the generator's compilation scan (typeof references in tests below + this comment).
    [AutoPack]
    public partial class TagSetEvent<T> where T : unmanaged
    {
        [Packed] public int EntityId;
        [Packed] public int Slot;
        [Packed] public T   Value;
    }

    // Closed-by-inheritance — the generator's inherited-fields walker should pick up
    // the substituted `int Value` from TagSetEvent<int> and emit HealthTagSetEvent.Packed
    // with that closed field type.
    public partial class HealthTagSetEvent : TagSetEvent<int> { }

    // --- Typed-accessor hierarchy: hand-written generic intermediate ---
    // Proposal's key case: [AutoPack] root → hand-written generic abstract intermediate
    // declaring abstract PackTyped/UnpackTyped → leaf with [Packed] fields gets `override` on typed.
    [AutoPack]
    public abstract partial class WireEnvelope { }

    public abstract class WireEnvelope<TPacked> : WireEnvelope, IPackable<TPacked>, IUnpackable<TPacked>
        where TPacked : unmanaged
    {
        public abstract TPacked PackTyped(IPackingHandler handler = null);
        public abstract void UnpackTyped(in TPacked packed, IPackingHandler handler = null);
    }

    public partial class WireEntityCreated : WireEnvelope<WireEntityCreated.Packed>
    {
        [Packed] public long EntityId;
    }

    // --- Walker smoke test: leaf reaches [AutoPack] root through a non-[AutoPack] generic intermediate ---
    [AutoPack]
    public abstract partial class WalkRoot { }

    public abstract class WalkMiddle<T> : WalkRoot where T : unmanaged
    {
        public abstract T GetIt();
    }

    public partial class WalkLeaf : WalkMiddle<WalkLeaf.Packed>
    {
        [Packed] public int X;
        public override WalkLeaf.Packed GetIt() => default;
    }

    // --- Generic-forwarding intermediate with own [Packed] fields ---
    // The intermediate is generic, has its own [Packed] fields, and forwards TPacked to a
    // hand-written generic abstract base (WireEnvelope<TPacked>). Without the suppression
    // branch, the generator would emit a nested Packed struct + PackTyped/UnpackTyped that
    // can't override the base's abstract typed methods (return type would be the wrong type).
    public abstract class TypedReqBase<TPacked, TPayload> : WireEnvelope<TPacked>
        where TPacked : unmanaged, IPackedStruct
    {
        [Packed] public int  Id;
        [Packed] public int  ParentId;
        [Packed] public bool Flag;
    }

    public sealed class SomeResponse { }

    public partial class TypedReqLeaf : TypedReqBase<TypedReqLeaf.Packed, SomeResponse>
    {
        [Packed] public int Extra;
    }

    // --- Zero-[Packed]-field concrete leaves: empty-payload codegen ---
    // A concrete (instantiable) [AutoPack] type whose aggregated [Packed] field set is empty
    // must still receive full codegen with an EMPTY Packed payload, so it can register as a
    // zero-data wire event (Network.Register<T, TPacked> constrains TPacked : unmanaged,
    // IPackedStruct). The discriminator is abstract vs. concrete leaf, not field count — the
    // abstract marker base WireEvent above must still get no Packed.

    // Concrete leaf of an [AutoPack] base, zero [Packed] fields (inherited `Trace` isn't [Packed]).
    public partial class SignalEvent : WireEvent { }

    // Closed-generic zero-field leaf: Foo<Bar>. The open definition declares no [Packed] fields;
    // the closed form must still get a Packed nested type whose PackedType resolves to Foo<Bar>.
    // The closed instantiation is discovered from the typeof/new references in the tests below.
    public struct Bar { }

    [AutoPack]
    public partial class Foo<T> { }

    public class AutoPackerTests
    {
        [Test]
        public void AutoPacker_Pack_GeneratesCorrectStructValues()
        {
            // Arrange
            var originalState = new PlayerState { Health = 42, Speed = 10.5f, SpawnPoint = new Vector3(1, 2, 3) };

            // Act
            IPackedStruct packedData = originalState.Pack(); 
            var concreteStruct = (PlayerState.Packed)packedData;
            
            // Assert
            Assert.AreEqual(42, concreteStruct.Health, "Health value should match the original struct.");
            Assert.AreEqual(10.5f, concreteStruct.Speed, "Speed value should match the original struct.");
            var expectedSpawnPoint = new Vector3(1, 2, 3);
            Assert.AreEqual(expectedSpawnPoint, concreteStruct.SpawnPoint, "SpawnPoint value should match the original struct.");
        }

        [Test]
        public void AutoPacker_Unpack_RestoresObjectStateCorrectly()
        {
            // Arrange
            var originalState = new PlayerState { Health = 99, Speed = 1.0f, TransientDescription = "Ignored Value" };

            IPackedStruct packedData = originalState.Pack(); 

            // Act
            var restoredState = new PlayerState((PlayerState.Packed)packedData);

            // Assert
            Assert.AreEqual(originalState.Health, restoredState.Health, "Health value should be completely restored.");
            Assert.AreNotEqual(originalState.TransientDescription, restoredState.TransientDescription, "Internal string wasn't packed, so it shouldn't be restored.");
            Assert.IsNull(restoredState.TransientDescription, "Transient properties should be default (null) upon reconstruction.");
        }

        [Test]
        public void AutoPacker_IUnpackable_RestoresObjectStateCorrectly()
        {
            var originalState = new PlayerState { Health = 77, Speed = 2.5f, SpawnPoint = new Vector3(4, 5, 6) };
            IPackedStruct packedData = originalState.Pack();

            IUnpackable restored = new PlayerState();
            restored.Unpack(packedData);

            var typed = (PlayerState)restored;
            Assert.AreEqual(77, typed.Health);
            Assert.AreEqual(2.5f, typed.Speed);
            Assert.AreEqual(new Vector3(4, 5, 6), typed.SpawnPoint);
        }

        [Test]
        public void AutoPacker_Pack_WithCustomHandler_UsesHandlerLogic()
        {
            // Arrange
            var payload = new SecurePayload { Value = "SecretMessage" };
            var customHandler = new MockEncryptionPacker();

            // Act
            IPackedStruct packedData = payload.Pack(customHandler);
            var concreteStruct = (SecurePayload.Packed)packedData;

            // Assert 
            Assert.AreEqual("SecretMessage".GetHashCode() + MockEncryptionPacker.ENCRYPTED_OFFSET, concreteStruct.Value, "Packer did not correctly intercept and modify the string into an int during Pack.");
        }
        
        [Test]
        public void AutoPacker_Unpack_WithCustomHandler_UsesHandlerLogic()
        {
            // Arrange
            var handlerContext = new MockEncryptionPacker();
            var payload = new SecurePayload { Value = "Decoded" }; // Arbitrary initial state
            
            // Simulate receiving the struct over a network with the matching Encoded int
            var networkData = new SecurePayload.Packed();
            networkData.Value = "SecretMessage".GetHashCode() + MockEncryptionPacker.ENCRYPTED_OFFSET;
            
            // Act
            var restoredPayload = new SecurePayload(networkData, handlerContext);

            // Assert
            Assert.AreEqual("SecretMessage", restoredPayload.Value, "Packer did not correctly intercept the integer and reconstruct the target string during Unpack.");
        }
        
        [Test]
        public void AutoPacker_Pack_WithExtensionMethod_UsesExtensionLogic()
        {
            // Arrange
            TestPackingExtensions.PackWasCalled = false;
            var payload = new ExtendedPayload { Coordinate = new Vector2(5, 5) };
            
            // The compiler natively binds to TestPackingExtensions.Resolve(this IPackingHandler handler, Vector2 source)

            // Act
            IPackedStruct packedData = payload.Pack();
            var concreteStruct = (ExtendedPayload.Packed)packedData;

            // Assert
            Assert.IsTrue(TestPackingExtensions.PackWasCalled, "The packing extension method was not invoked.");
            // 5 + 5 + 999 = 1009
            Assert.AreEqual(1009, concreteStruct.Coordinate, "Packer did not correctly invoke the implicitly bound extension method during Pack.");
        }
        
        [Test]
        public void AutoPacker_DerivedWithoutAttribute_GeneratesPackUnpack()
        {
            var original = new EntityCreatedEvent { EntityId = 1234 };
            IPackedStruct packed = original.Pack();

            IUnpackable restored = new EntityCreatedEvent();
            restored.Unpack(packed);

            Assert.AreEqual(1234, ((EntityCreatedEvent)restored).EntityId);
        }

        [Test]
        public void AutoPacker_AbstractMarkerBase_NoPackedStructEmitted()
        {
            // Abstract base with no own/inherited [Packed] fields gets only the
            // interface + abstract Pack/Unpack — no nested Packed struct.
            var nested = typeof(WireEvent).GetNestedType("Packed");
            Assert.IsNull(nested, "Abstract marker base should not get a generated Packed struct.");
        }

        [Test]
        public void AutoPacker_AbstractMarkerBase_AddsPackableInterfaces()
        {
            // The generator emits `: IPackable, IUnpackable` onto the base partial so the
            // user doesn't have to declare them by hand.
            Assert.IsTrue(typeof(IPackable).IsAssignableFrom(typeof(WireEvent)));
            Assert.IsTrue(typeof(IUnpackable).IsAssignableFrom(typeof(WireEvent)));
        }

        [Test]
        public void AutoPacker_AbstractMarkerBase_PolymorphicPackUnpack()
        {
            WireEvent ev = new EntityCreatedEvent { EntityId = 555 };
            IPackedStruct packed = ev.Pack();

            Assert.AreEqual(typeof(EntityCreatedEvent), packed.PackedType);

            WireEvent restored = new EntityCreatedEvent();
            restored.Unpack(packed);
            Assert.AreEqual(555, ((EntityCreatedEvent)restored).EntityId);
        }

        [Test]
        public void AutoPacker_DerivedInheritsPackedFields_RoundTrips()
        {
            var original = new DerivedStamped { Timestamp = 9999, Sequence = 7 };
            IPackedStruct packed = original.Pack();

            var derivedPacked = (DerivedStamped.Packed)packed;
            Assert.AreEqual(9999, derivedPacked.Timestamp, "Derived's Packed struct must include the inherited field.");
            Assert.AreEqual(7, derivedPacked.Sequence);

            IUnpackable restored = new DerivedStamped();
            restored.Unpack(packed);

            var typed = (DerivedStamped)restored;
            Assert.AreEqual(9999, typed.Timestamp);
            Assert.AreEqual(7, typed.Sequence);
        }

        [Test]
        public void AutoPacker_BaseAndDerivedBothCodegen_VirtualDispatchPicksDerived()
        {
            // Pack() called through base reference should dispatch to derived's Pack
            // (virtual on base, override on derived) and produce DerivedStamped.Packed.
            BaseStamped polymorphic = new DerivedStamped { Timestamp = 1, Sequence = 42 };
            IPackedStruct packed = polymorphic.Pack();

            Assert.AreEqual(typeof(DerivedStamped), packed.PackedType,
                "Virtual dispatch must select the derived Pack and produce DerivedStamped.Packed.");
        }

        [Test]
        public void AutoPacker_UserDeclaredAbstractBase_DerivedOverridesAndRoundTrips()
        {
            // User wrote the abstract base manually (Plan-C pattern).
            // Generator must NOT duplicate the abstract members on the base,
            // and the derived's Pack/Unpack must be `override` to satisfy the abstract contract.
            WireCommand cmd = new MoveCommand { Dx = 3, Dy = -4 };
            IPackedStruct packed = cmd.Pack();

            WireCommand restored = new MoveCommand();
            restored.Unpack(packed);

            var typed = (MoveCommand)restored;
            Assert.AreEqual(3, typed.Dx);
            Assert.AreEqual(-4, typed.Dy);
        }

        [Test]
        public void AutoPacker_ConsumerConfig1_BaseAttrOnly_RoundTrips()
        {
            // Proposal "Config 1" — [AutoPack] only on abstract base with explicit interfaces;
            // derived has [Packed] field but no [AutoPack]. The consumer reported this returns 0.
            var original = new CfgOneDerived { EntityId = 42 };
            IPackedStruct packed = original.Pack();

            var restored = new CfgOneDerived();
            ((IUnpackable)restored).Unpack(packed);

            Assert.AreEqual(42, restored.EntityId, "Config 1 round-trip must preserve EntityId.");
        }

        [Test]
        public void AutoPacker_ConsumerConfig2_BothAttrs_RoundTrips()
        {
            // Proposal "Config 2" — [AutoPack] on both base and derived. Consumer reported
            // override emission has a runtime bug; this confirms the body is correct.
            var original = new CfgTwoDerived { EntityId = 42 };
            IPackedStruct packed = original.Pack();

            var restored = new CfgTwoDerived();
            ((IUnpackable)restored).Unpack(packed);

            Assert.AreEqual(42, restored.EntityId, "Config 2 round-trip must preserve EntityId.");
        }

        [Test]
        public void AutoPacker_OpenGeneric_RoundTripsPerClosedInstantiation()
        {
            var src = new TagSetEvent<int> { EntityId = 1, Slot = 7, Value = 30 };
            var packed = (TagSetEvent<int>.Packed)src.Pack();
            Assert.AreEqual(1, packed.EntityId);
            Assert.AreEqual(7, packed.Slot);
            Assert.AreEqual(30, packed.Value);

            var dst = new TagSetEvent<int>();
            ((IUnpackable)dst).Unpack(packed);
            Assert.AreEqual(1, dst.EntityId);
            Assert.AreEqual(7, dst.Slot);
            Assert.AreEqual(30, dst.Value);
        }

        [Test]
        public void AutoPacker_OpenGeneric_DistinctClosedTypesHaveDistinctPackedStructs()
        {
            Assert.AreNotEqual(typeof(TagSetEvent<int>.Packed), typeof(TagSetEvent<float>.Packed));
            // Each closed Packed has the substituted Value field type.
            var intValue = typeof(TagSetEvent<int>.Packed).GetField("Value");
            var floatValue = typeof(TagSetEvent<float>.Packed).GetField("Value");
            Assert.AreEqual(typeof(int), intValue.FieldType);
            Assert.AreEqual(typeof(float), floatValue.FieldType);
        }

        [Test]
        public void AutoPacker_OpenGeneric_PackedTypeIsClosedForm()
        {
            var src = new TagSetEvent<float> { EntityId = 9, Slot = 2, Value = 1.5f };
            var packed = src.Pack();
            Assert.AreEqual(typeof(TagSetEvent<float>), packed.PackedType);
        }

        [Test]
        public void AutoPacker_OpenGeneric_ClosedByInheritance_RoundTrips()
        {
            var src = new HealthTagSetEvent { EntityId = 11, Slot = 3, Value = 42 };
            var packed = (HealthTagSetEvent.Packed)src.Pack();
            Assert.AreEqual(11, packed.EntityId);
            Assert.AreEqual(3, packed.Slot);
            Assert.AreEqual(42, packed.Value);
        }

        // --- Typed-accessor tests ---

        [Test]
        public void AutoPacker_PackTyped_ReturnsConcretePackedWithoutBoxing()
        {
            var src = new PlayerState { Health = 42, Speed = 10.5f, SpawnPoint = new Vector3(1, 2, 3) };
            PlayerState.Packed packed = src.PackTyped();
            Assert.AreEqual(42, packed.Health);
            Assert.AreEqual(10.5f, packed.Speed);
            Assert.AreEqual(new Vector3(1, 2, 3), packed.SpawnPoint);
        }

        [Test]
        public void AutoPacker_UnpackTyped_RestoresStateWithoutBoxing()
        {
            var src = new PlayerState { Health = 77, Speed = 2.5f, SpawnPoint = new Vector3(4, 5, 6) };
            PlayerState.Packed packed = src.PackTyped();

            var dst = new PlayerState();
            dst.UnpackTyped(packed);
            Assert.AreEqual(77, dst.Health);
            Assert.AreEqual(2.5f, dst.Speed);
            Assert.AreEqual(new Vector3(4, 5, 6), dst.SpawnPoint);
        }

        [Test]
        public void AutoPacker_TypedRoundTrip_WithCustomHandler_UsesHandlerLogic()
        {
            var handler = new MockEncryptionPacker();
            var src = new SecurePayload { Value = "SecretMessage" };
            SecurePayload.Packed packed = src.PackTyped(handler);
            Assert.AreEqual("SecretMessage".GetHashCode() + MockEncryptionPacker.ENCRYPTED_OFFSET, packed.Value);

            var dst = new SecurePayload();
            dst.UnpackTyped(packed, handler);
            Assert.AreEqual("SecretMessage", dst.Value);
        }

        [Test]
        public void AutoPacker_TypedInterfaces_DeclaredOnGeneratedPartial()
        {
            Assert.IsTrue(typeof(IPackable<PlayerState.Packed>).IsAssignableFrom(typeof(PlayerState)));
            Assert.IsTrue(typeof(IUnpackable<PlayerState.Packed>).IsAssignableFrom(typeof(PlayerState)));
        }

        [Test]
        public void AutoPacker_TypedInterfaces_NotEmittedOnAbstractMarker()
        {
            // The abstract marker has no nested Packed, so it can't implement IPackable<T>/IUnpackable<T>.
            foreach (var iface in typeof(WireEvent).GetInterfaces())
            {
                Assert.IsFalse(iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IPackable<>),
                    "Abstract marker base must not declare IPackable<T> — there is no Packed type for it.");
                Assert.IsFalse(iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IUnpackable<>),
                    "Abstract marker base must not declare IUnpackable<T>.");
            }
        }

        [Test]
        public void AutoPacker_HandWrittenAbstractTypedBase_DerivedOverridesAndRoundTrips()
        {
            // Generator must emit `override` on the leaf's PackTyped/UnpackTyped because the
            // hand-written WireEnvelope<TPacked> declares them abstract with TPacked closed at the leaf.
            var src = new WireEntityCreated { EntityId = 12345 };
            WireEntityCreated.Packed packed = src.PackTyped();
            Assert.AreEqual(12345, packed.EntityId);

            // Polymorphic call through the generic intermediate dispatches to the leaf's override.
            WireEnvelope<WireEntityCreated.Packed> envelope = src;
            WireEntityCreated.Packed viaBase = envelope.PackTyped();
            Assert.AreEqual(12345, viaBase.EntityId);

            var dst = new WireEntityCreated();
            ((WireEnvelope<WireEntityCreated.Packed>)dst).UnpackTyped(packed);
            Assert.AreEqual(12345, dst.EntityId);
        }

        [Test]
        public void AutoPacker_NonAutoPackGenericIntermediate_LeafGetsCodegen()
        {
            // [AutoPack] root → WalkMiddle<T> (no attribute) → WalkLeaf (no attribute).
            // The walker must traverse the non-[AutoPack] intermediate to reach the [AutoPack] root.
            var src = new WalkLeaf { X = 7 };
            WalkLeaf.Packed packed = src.PackTyped();
            Assert.AreEqual(7, packed.X);

            var dst = new WalkLeaf();
            dst.UnpackTyped(packed);
            Assert.AreEqual(7, dst.X);
        }

        [Test]
        public void AutoPacker_BoxedPack_DelegatesToPackTyped()
        {
            // The boxed Pack() is now a thin wrapper over PackTyped — both return the same value
            // (the wrapper boxes at the IPackedStruct return-type boundary).
            var src = new PlayerState { Health = 1, Speed = 2f, SpawnPoint = Vector3.zero };
            IPackedStruct boxed = src.Pack();
            PlayerState.Packed typed = src.PackTyped();
            Assert.AreEqual(typeof(PlayerState), boxed.PackedType);
            Assert.AreEqual(typed.Health, ((PlayerState.Packed)boxed).Health);
        }

        [Test]
        public void AutoPacker_DerivedTypedHidesBase_BothShapesAccessible()
        {
            // BaseStamped emits Packed{Timestamp}; DerivedStamped emits Packed{Timestamp, Sequence}.
            // Different return types → derived's PackTyped uses `new` to suppress CS0108.
            var d = new DerivedStamped { Timestamp = 100, Sequence = 9 };

            DerivedStamped.Packed derivedPacked = d.PackTyped();
            Assert.AreEqual(100, derivedPacked.Timestamp);
            Assert.AreEqual(9, derivedPacked.Sequence);

            // Through a BaseStamped reference, the inherited PackTyped returns BaseStamped.Packed
            // (no virtual relationship — `new` hides, not overrides).
            BaseStamped b = d;
            BaseStamped.Packed basePacked = b.PackTyped();
            Assert.AreEqual(100, basePacked.Timestamp);
        }

        [Test]
        public void AutoPacker_GenericIntermediate_WithPackedFields_AggregatesToLeaf()
        {
            var src = new TypedReqLeaf { Id = 7, ParentId = 3, Flag = true, Extra = 99 };
            TypedReqLeaf.Packed packed = src.PackTyped();
            Assert.AreEqual(7,    packed.Id);
            Assert.AreEqual(3,    packed.ParentId);
            Assert.AreEqual(true, packed.Flag);
            Assert.AreEqual(99,   packed.Extra);

            var dst = new TypedReqLeaf();
            dst.UnpackTyped(packed);
            Assert.AreEqual(7,    dst.Id);
            Assert.AreEqual(3,    dst.ParentId);
            Assert.AreEqual(true, dst.Flag);
            Assert.AreEqual(99,   dst.Extra);
        }

        [Test]
        public void AutoPacker_GenericIntermediate_DoesNot_Emit_OwnPackedStruct()
        {
            var nested = typeof(TypedReqBase<,>).GetNestedType("Packed");
            Assert.IsNull(nested, "Generic-forwarding intermediate must not emit own Packed struct.");
        }

        [Test]
        public void AutoPacker_GenericIntermediate_BoxedPack_RoundTrips()
        {
            var src = new TypedReqLeaf { Id = 5, ParentId = 1, Flag = false, Extra = 42 };
            IPackedStruct boxed = src.Pack();

            var dst = new TypedReqLeaf();
            ((IUnpackable)dst).Unpack(boxed);
            Assert.AreEqual(5,  dst.Id);
            Assert.AreEqual(1,  dst.ParentId);
            Assert.AreEqual(false, dst.Flag);
            Assert.AreEqual(42, dst.Extra);
        }

        [Test]
        public void AutoPacker_Unpack_WithExtensionMethod_UsesExtensionLogic()
        {
            // Arrange
            TestPackingExtensions.UnpackWasCalled = false;
            var payload = new ExtendedPayload();

            var networkData = new ExtendedPayload.Packed();
            networkData.Coordinate = 1009; // Represents Vector2(5, 5) -> 5 + 5 + 999 
            
            // Act
            var restoredPayload = new ExtendedPayload(networkData);

            // Assert
            Assert.IsTrue(TestPackingExtensions.UnpackWasCalled, "The unpacking extension method was not invoked.");
            var expectedCoordinate = new Vector2(5, 5);
            Assert.AreEqual(expectedCoordinate, restoredPayload.Coordinate, "Packer did not correctly invoke the implicitly bound extension method during Unpack.");
        }

        // --- Zero-[Packed]-field empty-payload codegen ---

        [Test]
        public void AutoPacker_ConcreteZeroFieldLeaf_GetsEmptyPackedStruct()
        {
            var nested = typeof(SignalEvent).GetNestedType("Packed");
            Assert.IsNotNull(nested, "Concrete zero-field leaf must still get a generated Packed struct.");

            var valueFields = nested.GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            Assert.AreEqual(0, valueFields.Length, "Zero-field leaf's Packed must carry no value fields (empty payload).");

            // Contrast: the abstract marker base it derives from still gets no Packed.
            Assert.IsNull(typeof(WireEvent).GetNestedType("Packed"),
                "Abstract marker base must still get no Packed struct — discriminator is abstract vs. concrete.");
        }

        [Test]
        public void AutoPacker_ConcreteZeroFieldLeaf_DeclaresTypedAndBoxedInterfaces()
        {
            Assert.IsTrue(typeof(IPackable).IsAssignableFrom(typeof(SignalEvent)));
            Assert.IsTrue(typeof(IUnpackable).IsAssignableFrom(typeof(SignalEvent)));
            Assert.IsTrue(typeof(IPackable<SignalEvent.Packed>).IsAssignableFrom(typeof(SignalEvent)));
            Assert.IsTrue(typeof(IUnpackable<SignalEvent.Packed>).IsAssignableFrom(typeof(SignalEvent)));
        }

        [Test]
        public void AutoPacker_ConcreteZeroFieldLeaf_RoundTripsZeroBytePayload()
        {
            var src = new SignalEvent();

            // Boxed path reports the concrete leaf type.
            IPackedStruct packed = src.Pack();
            Assert.AreEqual(typeof(SignalEvent), packed.PackedType, "Boxed Pack must report the concrete leaf type.");

            // Typed path round-trips a zero-byte payload without throwing.
            SignalEvent.Packed typed = src.PackTyped();
            Assert.AreEqual(typeof(SignalEvent), typed.PackedType);

            var dst = new SignalEvent();
            ((IUnpackable)dst).Unpack(packed);   // boxed unpack
            dst.UnpackTyped(typed);              // typed unpack
            Assert.AreEqual(typeof(SignalEvent), dst.PackTyped().PackedType);

            // ctor(Packed) reconstruction path also works for the empty payload.
            var viaCtor = new SignalEvent(typed);
            Assert.AreEqual(typeof(SignalEvent), viaCtor.PackTyped().PackedType);
        }

        [Test]
        public void AutoPacker_ClosedGenericZeroFieldLeaf_GetsEmptyPackedStruct()
        {
            var nested = typeof(Foo<Bar>).GetNestedType("Packed");
            Assert.IsNotNull(nested, "Closed-generic zero-field leaf must still get a generated Packed struct.");
        }

        [Test]
        public void AutoPacker_ClosedGenericZeroFieldLeaf_DeclaresTypedInterfaces()
        {
            Assert.IsTrue(typeof(IPackable<Foo<Bar>.Packed>).IsAssignableFrom(typeof(Foo<Bar>)));
            Assert.IsTrue(typeof(IUnpackable<Foo<Bar>.Packed>).IsAssignableFrom(typeof(Foo<Bar>)));
        }

        [Test]
        public void AutoPacker_ClosedGenericZeroFieldLeaf_RoundTripsAndReportsClosedPackedType()
        {
            var src = new Foo<Bar>();

            IPackedStruct packed = src.Pack();
            Assert.AreEqual(typeof(Foo<Bar>), packed.PackedType,
                "PackedType must resolve to the closed generic form, not the open definition.");

            Foo<Bar>.Packed typed = src.PackTyped();
            Assert.AreEqual(typeof(Foo<Bar>), typed.PackedType);

            var dst = new Foo<Bar>();
            ((IUnpackable)dst).Unpack(packed);
            dst.UnpackTyped(typed);
            Assert.AreEqual(typeof(Foo<Bar>), dst.PackTyped().PackedType);
        }
    }
}
