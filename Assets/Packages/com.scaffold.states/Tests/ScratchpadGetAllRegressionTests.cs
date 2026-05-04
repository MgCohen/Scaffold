#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Tests.Fixtures;

namespace Scaffold.States.Tests
{
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

        private sealed class CountAllCountersMutator : Mutator<CounterState>
        {
            private readonly List<int> counts;

            public CountAllCountersMutator(List<int> counts)
            {
                this.counts = counts;
            }

            public override CounterState Change(CounterState state, IStateScope scope)
            {
                int n = 0;
                foreach (CounterState _ in scope.GetAll<CounterState>())
                {
                    n++;
                }

                counts.Add(n);
                return state;
            }
        }

        [Test]
        public void PooledMutatorRunner_BackToBackExecute_GetAll_CountsMatchSliceCardinality()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            for (int i = 0; i < 5; i++)
            {
                builder.AddState(new SampleKey($"k{i}"), new CounterState(i));
            }

            Store store = builder.Build();

            var counts = new List<int>();
            store.ExecuteMutator(new CountAllCountersMutator(counts));
            store.ExecuteMutator(new CountAllCountersMutator(counts));

            Assert.That(counts, Is.EqualTo(new[] { 6, 6 }));
        }
    }
}
