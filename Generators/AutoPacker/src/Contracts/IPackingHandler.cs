using System;

public interface IPackingHandler
{
    TTarget Resolve<TSource, TTarget>(TSource source);
}
