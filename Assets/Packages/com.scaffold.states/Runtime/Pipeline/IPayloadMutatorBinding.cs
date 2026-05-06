#nullable enable

namespace Scaffold.States
{
    internal interface IPayloadMutatorBinding
    {
        void Apply(object payload, MutatorRunner runner, Reference executeReference);
    }
}
