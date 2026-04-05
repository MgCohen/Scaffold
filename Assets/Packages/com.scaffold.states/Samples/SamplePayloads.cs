#nullable enable

using Scaffold.States;

namespace Scaffold.States.Samples
{
    /// <summary>
    /// Single payload type handled by multiple registered mutators (see <see cref="SampleStoreFactory"/>).
    /// </summary>
    public sealed record CombinedTickPayload(int Delta);

    /// <summary>
    /// Routes to the keyed <see cref="CounterState"/> slice when the mutator is registered with <see cref="Scaffold.States.Reference.Null"/>.
    /// </summary>
    public sealed record RoutedCounterPayload(SampleKey Target, int Delta) : IPayloadReference
    {
        public IReference GetReference() => Target;
    }
}
