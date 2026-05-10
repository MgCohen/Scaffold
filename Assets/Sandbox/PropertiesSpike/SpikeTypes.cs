using Unity.Properties;
using UnityEngine;

namespace Scaffold.GraphFlow.PropertiesSpike
{
    // Throwaway payload used by E1/E2/E3. Mixes attribute permutations so
    // AttributesTest can verify each kind survives onto the generated
    // IProperty.HasAttribute<T>(). Untagged InternalCounter exists so we can
    // confirm absence of attributes is also reported correctly.
    [GeneratePropertyBag]
    public class SpikeEvent
    {
        [In]              public int Health;
        [In]              public string Name = string.Empty;
        [Out]             public Vector3 Position;
        [GraphPort]       public float Damage;
        [GraphPortIgnore] public bool Hit;
        public int InternalCounter;
    }

    [GeneratePropertyBag]
    public class SpikeCommand
    {
        [In]  public int Amount;
        [Out] public bool Success;
    }

    [GeneratePropertyBag]
    public class SpikeEntry
    {
        [In]  public string TriggerId = string.Empty;
        [Out] public int Result;
    }
}
