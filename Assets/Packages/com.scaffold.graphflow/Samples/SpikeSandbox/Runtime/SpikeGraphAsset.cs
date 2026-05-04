using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.Spike
{
    // Hand-written by design (per the existing pattern): Unity binds ScriptableObject types to
    // MonoScripts via on-disk .cs files; generator-emitted virtual sources don't satisfy that.
    // The type stem must be SpikeRunner → "Spike" → SpikeGraphAsset to match what the
    // generator-emitted SpikeGraphImporter expects.
    public sealed class SpikeGraphAsset : GraphAsset<SpikeRunner> { }
}
