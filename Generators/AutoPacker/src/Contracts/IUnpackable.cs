namespace Scaffold.AutoPacker
{
    public interface IUnpackable
    {
        void Unpack(IPackedStruct packed, IPackingHandler handler = null);
    }

    public interface IUnpackable<TPacked> : IUnpackable where TPacked : unmanaged
    {
        void UnpackTyped(in TPacked packed, IPackingHandler handler = null);
    }
}
