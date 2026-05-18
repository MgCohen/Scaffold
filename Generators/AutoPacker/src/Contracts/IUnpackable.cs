namespace Scaffold.AutoPacker
{
    public interface IUnpackable
    {
        void Unpack(IPackedStruct packed, IPackingHandler handler = null);
    }
}
