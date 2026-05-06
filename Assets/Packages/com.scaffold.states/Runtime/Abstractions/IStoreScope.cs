#nullable enable

namespace Scaffold.States
{
    public interface IStoreScope : IStateScope
    {
        IStateEventHandler Events { get; }

        ICatalog Catalog { get; }
    }
}
