using System;
using Unity.GraphToolkit.Editor;

namespace Scaffold.EffectGraph.Editor
{
    /// <summary>
    /// Consumer graphs subclass this with their runner type and <c>[Graph("extension")]</c>.
    /// </summary>
    [Serializable]
    public abstract class Graph<TRunner> : Graph where TRunner : Runtime.GraphRunner
    {
    }
}
