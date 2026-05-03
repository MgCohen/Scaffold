using Scaffold.GraphFlow.M0.Editor;
using Scaffold.GraphFlow.M0.Editor.GToolkit;
using Scaffold.GraphFlow.M0.Smoke;

namespace Scaffold.GraphFlow.M0.Generated
{
    // IntToString is a pure data node (no IGraphEntry / IGraphAction / IExecutable / [GraphCommandPair] marker),
    // so the generator doesn't see it. Hand-register it through the partial RegisterAdditional hook.
    public static partial class MySmokeGraphRegistry
    {
        static partial void RegisterAdditional(GraphPackageRegistry<MySmokeRunner> r)
        {
            r.Register(new GraphPackageRegistry<MySmokeRunner>.NodeRegistration
            {
                EditorNodeType = typeof(IntToStringEditorNode),
                Factory = _ => new IntToStringRuntime(),
                DataInputPortIds = { [IntToStringEditorNode.InValuePortName] = IntToStringRuntime.Ports.InValue },
                DataOutputPortIds = { [IntToStringEditorNode.OutResultPortName] = IntToStringRuntime.Ports.OutString },
            });
        }
    }
}
