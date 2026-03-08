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
    
        // Pack: Vector2 -> Int
        public static int Resolve(this IPackingHandler handler, Vector2 source)
        {
            PackWasCalled = true;
            return (int)(source.x + source.y) + VECTOR_HASH_OFFSET;
        }
        
        // Unpack: Int -> Vector2
        public static Vector2 Resolve(this IPackingHandler handler, int source)
        {
            UnpackWasCalled = true;
            // For testing purposes, we loosely reconstruct it back into a coordinate. 
            float val = (source - VECTOR_HASH_OFFSET) / 2f;
            return new Vector2(val, val);
        }
    }

    public class AutoPackerTests
    {
        [Test]
        public void AutoPacker_Pack_GeneratesCorrectStructValues()
        {
            // Arrange
            var originalState = new PlayerState
            {
                Health = 42,
                Speed = 10.5f,
                SpawnPoint = new Vector3(1, 2, 3)
            };

            // Act
            IPackedStruct packedData = originalState.Pack(); 
            var concreteStruct = (PlayerState.Packed)packedData;
            
            // Assert
            Assert.AreEqual(42, concreteStruct.Health, "Health value should match the original struct.");
            Assert.AreEqual(10.5f, concreteStruct.Speed, "Speed value should match the original struct.");
            Assert.AreEqual(new Vector3(1, 2, 3), concreteStruct.SpawnPoint, "SpawnPoint value should match the original struct.");
        }

        [Test]
        public void AutoPacker_Unpack_RestoresObjectStateCorrectly()
        {
            // Arrange
            var originalState = new PlayerState
            {
                Health = 99,
                Speed = 1.0f,
                TransientDescription = "Ignored Value"
            };

            IPackedStruct packedData = originalState.Pack(); 

            // Act
            var restoredState = new PlayerState((PlayerState.Packed)packedData);

            // Assert
            Assert.AreEqual(originalState.Health, restoredState.Health, "Health value should be completely restored.");
            Assert.AreNotEqual(originalState.TransientDescription, restoredState.TransientDescription, "Internal string wasn't packed, so it shouldn't be restored.");
            Assert.IsNull(restoredState.TransientDescription, "Transient properties should be default (null) upon reconstruction.");
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
            Assert.AreEqual(new Vector2(5, 5), restoredPayload.Coordinate, "Packer did not correctly invoke the implicitly bound extension method during Unpack.");
        }
    }
}
