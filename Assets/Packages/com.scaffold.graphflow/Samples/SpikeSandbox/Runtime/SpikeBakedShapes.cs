using System;

namespace Scaffold.GraphFlow.Spike
{
    /// <summary>
    /// Polymorphic base for the Phase 3 spike — stand-in for whatever the production OnTrigger /
    /// Return baked instance would carry. The point: it's a polymorphic <c>[Serializable]</c>
    /// hierarchy stored as <c>[SerializeReference]</c> on a <c>Node</c> subclass field.
    /// </summary>
    [Serializable]
    public abstract class SpikeBakedShape
    {
        public abstract string Describe();
    }

    [Serializable]
    public sealed class SpikeBakedDamageShape : SpikeBakedShape
    {
        public int FieldCount;
        public string PickedTypeName = "";

        public override string Describe()
            => $"DamageShape(picked={PickedTypeName}, fieldCount={FieldCount})";
    }

    [Serializable]
    public sealed class SpikeBakedHealShape : SpikeBakedShape
    {
        public string Note = "";

        public override string Describe()
            => $"HealShape(note={Note})";
    }
}
