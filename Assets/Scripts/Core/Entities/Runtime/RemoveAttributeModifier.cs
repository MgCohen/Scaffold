using System;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class RemoveAttributeModifier : EntityModifier
    {
        public double Amount;

        public override double Apply(double currentValue)
        {
            return currentValue - Amount;
        }
    }
}
