using UnityEngine;

namespace Scaffold.Entities.Samples
{
    /// <summary>
    /// Sample <see cref="EntityDefinition"/> with a Create menu entry for authoring in the Samples folder.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Scaffold/Samples/Sample Character Definition",
        fileName = "SampleCharacterDefinition",
        order = 0)]
    public sealed class SampleCharacterDefinition : EntityDefinition
    {
    }
}
