#nullable enable

using Scaffold.States;

namespace Scaffold.States.Samples
{
    /// <summary>
    /// Minimal keyed reference for multi-slice samples (implements <see cref="IReference"/>).
    /// </summary>
    public sealed record SampleKey(string Name) : IReference;
}
