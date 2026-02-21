using System;

public interface ISerializationResolver
{
    TTarget Resolve<TSource, TTarget>(TSource source);
}
