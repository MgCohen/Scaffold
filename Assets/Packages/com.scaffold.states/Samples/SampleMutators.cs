#nullable enable

using Scaffold.States;

namespace Scaffold.States.Samples
{
    public sealed class IncrementCounterMutator : Mutator<CounterState>
    {
        private readonly int amount;

        public IncrementCounterMutator(int amount)
        {
            this.amount = amount;
        }

        public override CounterState Change(CounterState state, IStateScope scope)
        {
            return new CounterState(state.Value + amount);
        }
    }

    public sealed class ApplyCombinedTickToCounter : Mutator<CounterState, CombinedTickPayload>
    {
        public override CounterState Change(CounterState state, CombinedTickPayload payload, IStateScope scope)
        {
            return new CounterState(state.Value + payload.Delta);
        }
    }

    public sealed class ApplyCombinedTickToNotes : Mutator<NotesState, CombinedTickPayload>
    {
        public override NotesState Change(NotesState state, CombinedTickPayload payload, IStateScope scope)
        {
            return new NotesState(state.Text + new string('a', payload.Delta));
        }
    }

    public sealed class AddDeltaToKeyedCounter : Mutator<CounterState, RoutedCounterPayload>
    {
        public override CounterState Change(CounterState state, RoutedCounterPayload payload, IStateScope scope)
        {
            return new CounterState(state.Value + payload.Delta);
        }
    }
}
