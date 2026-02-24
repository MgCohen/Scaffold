using System;

public interface IPackable
{
    IPackedStruct Pack(IPackingHandler resolver = null);
}
