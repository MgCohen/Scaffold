#nullable enable

using Scaffold.States;

namespace Scaffold.States.Samples
{
    public sealed record CombinedTickPayload(int Delta);

    public sealed record RoutedCounterPayload(SampleKey Target, int Delta) : IPayloadReference
    {
        public Reference GetReference() => Target;
    }
}
