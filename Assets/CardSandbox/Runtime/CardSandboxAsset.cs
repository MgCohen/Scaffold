#nullable enable
using Scaffold.GraphFlow.M0;

using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Hand-written GraphAsset carrier for the card sandbox. Unity's MonoScript binding for
    /// ScriptableObject types requires a real on-disk .cs file with a matching name, so even in
    /// the runtime-only sketch we keep the carrier as a plain class. In the full M3 path the
    /// generator-emitted importer + graph trio targets this same asset type.
    /// </summary>
    public sealed class CardSandboxAsset : GraphAsset<CardEffectRunner> { }
}
