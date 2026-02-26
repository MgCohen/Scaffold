using System;

namespace Scaffold.AutoPacker
{
    public interface IPackable
    {
        IPackedStruct Pack(IPackingHandler resolver = null);
    }
}
