using System.Collections.Generic;

namespace Scaffold.Entities
{
    public static class BuiltinAttributeDefinitions
    {
        public static readonly FloatAttributeDefinition Float = new FloatAttributeDefinition();

        public static readonly IntAttributeDefinition Int = new IntAttributeDefinition();

        public static readonly BoolAttributeDefinition Bool = new BoolAttributeDefinition();

        public static readonly StringAttributeDefinition String = new StringAttributeDefinition();

        public static readonly IReadOnlyList<IAttributeDefinition> All = new IAttributeDefinition[]
        {
            Float,
            Int,
            Bool,
            String
        };
    }
}
