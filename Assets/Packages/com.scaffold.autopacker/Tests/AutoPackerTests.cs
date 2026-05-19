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
    }
}
