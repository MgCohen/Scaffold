using Unity.Properties;

namespace Scaffold.GraphFlow.PropertiesSpike
{
    // Day 2 will fill this in for the E3 perf experiment: a typed visitor
    // that copies fields container-to-container via Property<T,V>.GetValue /
    // SetValue, used as the comparison against direct hand-rolled assignment.
    // Empty subclass compiles because PropertyVisitor's hooks are all virtual.
    internal sealed class SpikePortBinder : PropertyVisitor
    {
    }
}
