using System.Collections.Generic;

namespace Scaffold.Entities
{
    public static class EntityVariableComputer
    {
        public static VariableValue ComputeEffective(VariableValue baseValue, IReadOnlyList<VariableValue> contributions)
        {
            if (baseValue == null)
            {
                return null;
            }

            if (contributions == null || contributions.Count == 0)
            {
                return baseValue;
            }

            return baseValue.Combine(contributions);
        }
    }
}
