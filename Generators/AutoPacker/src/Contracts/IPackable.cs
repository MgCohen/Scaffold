namespace Scaffold.AutoPacker
{
    public interface IPackable
    {
        IPackedStruct Pack(IPackingHandler resolver = null);
    }

    public interface IPackable<TPacked> : IPackable where TPacked : unmanaged
    {
        TPacked PackTyped(IPackingHandler handler = null);
    }
}
