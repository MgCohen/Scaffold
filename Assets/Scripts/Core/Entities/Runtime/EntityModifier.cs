using System;

namespace Scaffold.Entities
{
    [Serializable]
    public abstract class EntityModifier
    {
        public abstract double Apply(double currentValue);
    }
}
