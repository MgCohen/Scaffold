using System;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class AddAttributeModifier : EntityModifier
    {
        public double Amount;

        public override double Apply(double currentValue)
        {
            return currentValue + Amount;
        }
    }
}
