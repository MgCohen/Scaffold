using System;

namespace Scaffold.AutoPacker
{
    public class DefaultPackingHandler : IPackingHandler
    {
        public TTarget Resolve<TSource, TTarget>(TSource source)
        {
            if (source == null) return default;
            if (source is TTarget target) return target;
            return (TTarget)Convert.ChangeType(source, typeof(TTarget));
        }
    }
}
