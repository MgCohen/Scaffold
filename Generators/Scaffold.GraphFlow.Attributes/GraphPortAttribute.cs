using System;

namespace Scaffold.GraphFlow
{
    /// <summary>Optional stable port id override (rename-safe).</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class GraphPortAttribute : Attribute
    {
        public int Id { get; set; }
    }
}
