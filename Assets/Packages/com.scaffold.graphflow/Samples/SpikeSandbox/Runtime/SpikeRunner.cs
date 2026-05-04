using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.Spike
{
    /// <summary>Throwaway runner for the GraphToolkit-API spike. No services — just satisfies the
    /// <c>[GraphPackage].Runner</c> requirement so the generator emits a Graph + Importer we can
    /// open in the editor.</summary>
    public sealed class SpikeRunner : GraphRunner
    {
    }
}
