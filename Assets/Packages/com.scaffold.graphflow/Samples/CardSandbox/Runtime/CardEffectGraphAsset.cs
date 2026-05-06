#nullable enable
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Sealed GraphAsset carrier for the card sandbox. Name matches the generator's emitted importer
    /// expectation: stem <c>CardEffect</c> (derived from <c>CardEffectRunner</c> by
    /// <c>RunnerStem.FromTypeName</c>) → <c>CardEffectGraphAsset</c>. Hand-written because Unity's
    /// MonoScript binding for <c>ScriptableObject</c> sub-assets needs an on-disk .cs file with a
    /// matching type name; generator-emitted virtual files don't satisfy that lookup. Tests that
    /// build assets in code (BuildAsset factories) also use this concrete type.
    /// </summary>
    public sealed class CardEffectGraphAsset : GraphAsset<CardEffectRunner> { }
}
