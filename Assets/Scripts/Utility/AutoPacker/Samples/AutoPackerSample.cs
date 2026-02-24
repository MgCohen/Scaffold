using UnityEngine;
using Scaffold.AutoPacker;

namespace Scaffold.AutoPacker.Samples
{
    /// <summary>
    /// This sample demonstrates how to use the AutoPacker to generate a packed struct for a class.
    /// The generated 'Packed' struct is unmanaged and can be easily serialized or sent over the network.
    /// </summary>
    [AutoPack]
    public partial class CharacterStats
    {
        [Packed]
        public int Health;

        [Packed]
        public int Mana;

        [Packed]
        public float Speed;

        [Packed(typeof(int))]
        public float ExpMultiplier; // Pack as int (e.g., scaled by 100)

        public CharacterStats() { }

        public CharacterStats(int health, int mana, float speed, float expMultiplier)
        {
            Health = health;
            Mana = mana;
            Speed = speed;
            ExpMultiplier = expMultiplier;
        }

        public void PrintStats()
        {
            Debug.Log($"Stats - Health: {Health}, Mana: {Mana}, Speed: {Speed}, ExpMultiplier: {ExpMultiplier}");
        }
    }

    /// <summary>
    /// A custom packing handler that scales floats when packing into ints.
    /// This is useful for fixed-point math or reducing precision for networking.
    /// </summary>
    public class ScaledPackingHandler : IPackingHandler
    {
        private const float Scale = 100f;

        public TTarget Resolve<TSource, TTarget>(TSource source)
        {
            // When packing: float -> int
            if (source is float f && typeof(TTarget) == typeof(int))
            {
                return (TTarget)(object)Mathf.RoundToInt(f * Scale);
            }

            // When unpacking: int -> float
            if (source is int i && typeof(TTarget) == typeof(float))
            {
                return (TTarget)(object)(i / Scale);
            }

            // Fallback to default conversion
            return (TTarget)System.Convert.ChangeType(source, typeof(TTarget));
        }
    }

    public class AutoPackerSample : MonoBehaviour
    {
        private void Start()
        {
            // 1. Create a packable object
            var stats = new CharacterStats(100, 50, 5.5f, 1.25f);
            stats.PrintStats();

            // 2. Pack it into the generated struct using a custom handler
            // The ExpMultiplier is decorated with [Packed(typeof(int))], 
            // so ScaledPackingHandler will scale it by 100.
            var handler = new ScaledPackingHandler();
            var packed = (CharacterStats.Packed)stats.Pack(handler);
            Debug.Log($"Packed Data - Health: {packed.Health}, Mana: {packed.Mana}, Speed: {packed.Speed}, ExpMultiplier (Scaled Int): {packed.ExpMultiplier}");

            // 3. Restore from packed data using the same handler
            var restoredStats = new CharacterStats(packed, handler);
            restoredStats.PrintStats();
        }
    }
}
