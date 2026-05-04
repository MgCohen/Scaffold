#nullable enable

namespace Scaffold.States
{
    public interface IMutatorDispatcher
    {
        bool TryDispatch<TPayload>(Store store, Reference reference, TPayload payload);
    }
}
