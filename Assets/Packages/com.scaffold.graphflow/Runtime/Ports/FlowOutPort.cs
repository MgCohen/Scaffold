#nullable enable

namespace Scaffold.GraphFlow
{
    public sealed class FlowOutPort : Port
    {
        public static readonly FlowOutPort End = new(null!, "<end>");

        public RuntimeNode Owner { get; }
        public string Name { get; }
        public FlowConnection? Connection { get; internal set; }

        public FlowOutPort(RuntimeNode owner, string name)
        {
            Owner = owner;
            Name = name;
        }
    }
}
