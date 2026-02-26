using System;

namespace Scaffold.AutoPacker
{
    public interface IPackingHandler
    {
        TTarget Resolve<TSource, TTarget>(TSource source);
    }
}
