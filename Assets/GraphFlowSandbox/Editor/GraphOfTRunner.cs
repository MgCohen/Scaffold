using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.M0.Editor.GToolkit
{
    /// <summary>
    /// Package base for generator-emitted graph subclasses — validates GT accepts generic inheritance.
    /// </summary>
    public abstract class Graph<TRunner> : Unity.GraphToolkit.Editor.Graph where TRunner : Scaffold.GraphFlow.M0.GraphRunner
    {
    }
}
