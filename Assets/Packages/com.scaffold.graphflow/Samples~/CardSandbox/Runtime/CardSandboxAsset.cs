#nullable enable
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Sealed GraphAsset carrier for the card sandbox. <c>GraphAsset&lt;TRunner&gt;</c> is abstract
    /// and Unity's <c>ScriptableObject.CreateInstance</c> requires a concrete type, so the sample
    /// keeps a tiny sealed subclass even though no editor-side baking happens here — every test
    /// builds its asset in code.
    /// </summary>
    public sealed class CardSandboxAsset : GraphAsset<CardEffectRunner> { }
}
