#nullable enable
using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Non-generic dispatch interface for typed entry runtimes. Generator emits one concrete
    /// <see cref="IEntryBridge"/> per entry payload so <see cref="GraphController{TRunner}"/>
    /// can dispatch a payload by <see cref="Type"/> with zero reflection on hot or hydration paths.
    /// </summary>
    public interface IEntryBridge
    {
        Type PayloadType { get; }
        Task<Flow> Run(object payload);
    }
}
