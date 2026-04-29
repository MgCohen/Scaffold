#nullable enable

using NUnit.Framework;

using Scaffold.States;

namespace Scaffold.States.Tests
{
    public sealed class MutatorRegistryDeduplicationTests
    {
        private sealed record TestState(int Value) : State;

        private sealed record TestPayload();

        private sealed class TestMutator : Mutator<TestState, TestPayload>
        {
            public override TestState Change(TestState state, TestPayload payload, IStateScope scope) => state;
        }

        [Test]
        public void RegisterTwice_ThrowsDuplicateMutatorRegistrationException()
        {
            var registry = new MutatorRegistry();
            registry.Register(new TestMutator());
            Assert.Throws<DuplicateMutatorRegistrationException>(() => registry.Register(new TestMutator()));
        }
    }
}
