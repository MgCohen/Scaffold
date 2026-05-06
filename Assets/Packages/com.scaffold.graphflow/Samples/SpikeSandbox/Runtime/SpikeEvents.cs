namespace Scaffold.GraphFlow.Spike
{
    /// <summary>
    /// Stand-in for a real <c>[GraphEvent]</c>-tagged class. Phase 2.5 spike — the generator would
    /// discover all such classes per package and emit an enum + mapping switch from them.
    /// </summary>
    public sealed class SpikeDamage
    {
        public int Amount;
        public string Target = "";
        public bool Critical;
    }

    /// <summary>Second sample event — different fields so the dynamic-port emit is visibly different.</summary>
    public sealed class SpikeHeal
    {
        public int Amount;
        public string Source = "";
    }
}
