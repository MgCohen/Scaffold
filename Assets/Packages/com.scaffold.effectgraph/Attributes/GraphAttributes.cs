using System;

namespace Scaffold.EffectGraph
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class OutAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class GraphHiddenAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class GraphPortAttribute : Attribute
    {
        public int Order { get; set; }
        public string? DisplayName { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class GraphMenuAttribute : Attribute
    {
        public string Path { get; }

        public GraphMenuAttribute(string path) => Path = path;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class NoGraphNodeAttribute : Attribute { }
}
