#nullable enable

using Scaffold.States;

namespace Scaffold.States.Samples
{
    public sealed record CounterState(int Value) : State;

    public sealed record NotesState(string Text) : State;

    public sealed record TotalsDashboardState(int CounterValue, int NoteCharacterCount) : AggregateState;
}
