#nullable enable

namespace Scaffold.Entities.States
{
    // Controls what StoreVariableBagBuilder does when a graph-declared variable
    // (RuntimeVariable in the seed) has no explicit Bind / BindReadOnly / BindBase.
    // InMemoryDefault materializes a plain InMemoryHandle from the seed default
    // so the variable still works (just outside store-coherence). Throw makes
    // Build() fail loudly, catching authoring mistakes early.
    public enum FallbackMode
    {
        InMemoryDefault,
        Throw,
    }
}
