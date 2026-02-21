using System;

public class DefaultSerializationResolver : ISerializationResolver
{
    public TTarget Resolve<TSource, TTarget>(TSource source)
    {
        if (source == null) return default;
        if (source is TTarget target) return target;
        return (TTarget)Convert.ChangeType(source, typeof(TTarget));
    }
}
