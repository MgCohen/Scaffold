#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Samples;

namespace Scaffold.States.Tests
{
    /// <summary>
    /// Regression: mutator <see cref="IStateScope.GetAll{TState}"/> (backed by the store scratchpad) must only
    /// enumerate references that have a row for that state type. Enumerating every distinct primary key in the
    /// map incorrectly included <see cref="Reference.Null"/> from unrelated state types and led to
    /// <see cref="KeyNotFoundException"/> when resolving a type that is never registered at null.
    /// </summary>
    public sealed class ScratchpadGetAllRegressionTests
    {
        private sealed class CollectKeyedCanonicalMutator : Mutator<CounterState>
        {
            private readonly List<CounterState> sink;

            public CollectKeyedCanonicalMutator(List<CounterState> sink)
            {
                this.sink = sink;
            }

            public override CounterState Change(CounterState state, IStateScope scope)
            {
                sink.AddRange(scope.GetAll<CounterState>());
                return state;
            }
        }

        [Test]
        public void Scratchpad_GetAll_OnlyEnumeratesReferencesForThatState_DoesNotThrowWhenNullUsedByOtherTypes()
        {
            var keyA = new SampleKey("A");
            var builder = new StoreBuilder();
            builder.AddState(new NotesState("note")); // Reference.Null row for NotesState only
            builder.AddState(keyA, new CounterState(42)); // CounterState only at keyA, not at Reference.Null

            Store store = builder.Build();

            var collected = new List<CounterState>();
            Assert.DoesNotThrow(() => store.ExecuteMutator<CounterState>(keyA, new CollectKeyedCanonicalMutator(collected)));

            Assert.That(collected, Has.Count.EqualTo(1));
            Assert.That(collected[0].Value, Is.EqualTo(42));
        }
    }
}
