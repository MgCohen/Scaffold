#nullable enable

namespace Scaffold.States
{
    /// <summary>
    /// Read access to state plus the store event surface (for aggregate subscription wiring at build time).
    /// </summary>
    public interface IStoreScope : IStateScope
    {
        IStateEventHandler Events { get; }
    }
}
