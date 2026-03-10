using System;

namespace Scaffold.Entities
{
    [Serializable]
    public class MultiplyAttributeModifier : EntityModifier
    {
        public double Factor = 1d;

        public override double Apply(double currentValue)
        {
            return currentValue * Factor;
        }
    }
}
