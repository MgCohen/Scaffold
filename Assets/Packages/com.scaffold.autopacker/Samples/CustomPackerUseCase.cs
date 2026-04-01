using System;
using UnityEngine;
using Scaffold.AutoPacker;

namespace Scaffold.Autopacker.Samples
{
    [AutoPack]
    public partial class SecureData
    {
        [Packed] public int Id;
        [Packed(typeof(int))] public string Secret;
        [Packed(typeof(int))] public Vector2 Secret2;
    }

    /// <summary>
    /// A custom packing handler allows you to intercept packing and unpacking logic.
    /// You could use this to encrypt specific properties or run compression algorithms!
    /// </summary>
    public class EncryptionPacker : IPackingHandler
    {
        public TTarget Resolve<TSource, TTarget>(TSource source)
        {
            if (source == null) return default;
            
            // On Pack: String -> Int (hash it)
            // On Unpack: Int -> String (lookup or format)
            if (source is string text && typeof(TTarget) == typeof(int))
            {
                return (TTarget)(object)text.GetHashCode();
            }
            if (source is int hash && typeof(TTarget) == typeof(string))
            {
                // Simple unpacking demonstration
                return (TTarget)(object)("Decoded_" + hash);
            }

            // Fallback to default conversion for everything else (like the int Id)
            if (source is TTarget target) return target;
            return (TTarget)Convert.ChangeType(source, typeof(TTarget));
        }
    }

    public static class PackingExtensions
    {
        public static int Resolve(this IPackingHandler handler, Vector2 source)
        {
            return default;
        }
    }

    public class CustomPackerUseCase : MonoBehaviour
    {
        private void Start()
        {
            var data = new SecureData { Id = 1, Secret = "MySuperSecretValue" };

            Debug.Log($"[Original] Id={data.Id}, Secret={data.Secret}");

            // 1. Instantiate our Custom Handler
            var encoder = new EncryptionPacker();

            // 2. Use the generated .Pack method, supplying our handler!
            //    Our handler converts the string during packing.
            IPackedStruct packedData = data.Pack(encoder);

            // 3. Inspecting the raw struct reveals our injected logic performed work!
            var rawSecretExtracted = ((SecureData.Packed)packedData).Secret;
            Debug.Log($"[Packed (Intercepted)] Id={((SecureData.Packed)packedData).Id}, SecretHash={rawSecretExtracted}");
            
            // 4. Decode the data via the Constructor, supplying the same handler so it reverses the logic back to expected!
            var restoredData = new SecureData((SecureData.Packed)packedData, encoder);

            Debug.Log($"[Restored] Id={restoredData.Id}, Secret={restoredData.Secret}");
        }
    }
}
