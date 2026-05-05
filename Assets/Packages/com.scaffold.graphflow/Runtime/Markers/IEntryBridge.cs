#nullable enable
using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public interface IEntryBridge
    {
        Type PayloadType { get; }
        Task<Flow> Run(object payload);
    }
}
