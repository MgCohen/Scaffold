using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor
{
    public static class PortValueResolver
    {
        public static bool TryResolve<T>(IPort port, out T value)
        {
            value = default;
            if (port == null) return false;

            if (!port.isConnected)
                return port.TryGetValue(out value);

            var source = port.firstConnectedPort?.GetNode();
            if (source is IConstantNode cn)
                return cn.TryGetValue(out value);
            if (source is IVariableNode vn)
                return vn.variable.TryGetDefaultValue(out value);

            return false;
        }
    }
}
