using System;
using System.Collections.Generic;
namespace Scaffold.MVVM.Binding
{
    internal class BindSet<TSource, TTarget> : IBindSet<TSource, TTarget>
    {
        private readonly List<Converter<TSource, TTarget>> converters = new List<Converter<TSource, TTarget>>();
        private readonly List<Adapter<TTarget>> adapters = new List<Adapter<TTarget>>();

        public void RegisterConverter(Converter<TSource, TTarget> converter)
        {
            if (converter is null)
{
    throw new ArgumentNullException(nameof(converter));
}
            converters.Add(converter);
        }

        public void RegisterAdapter(Adapter<TTarget> adapter)
        {
            if (adapter is null)
{
    throw new ArgumentNullException(nameof(adapter));
}
            adapters.Add(adapter);
        }

        public bool TryConvert(TSource source, out TTarget target)
        {
            if (converters.Count == 0)
            {
                target = default;
                return false;
            }
            return TryConvertWithConverters(source, out target);
        }

        private bool TryConvertWithConverters(TSource source, out TTarget target)
        {
            foreach (var converter in converters)
            {
                if (!converter.CanConvert(source)) continue;
                target = converter.Convert(source);
                return true;
            }
            target = default;
            return false;
        }

        public bool TryAdapt(TTarget target, out TTarget newTarget)
        {
            if (adapters.Count == 0)
            {
                newTarget = default;
                return false;
            }
            return TryAdaptWithAdapters(target, out newTarget);
        }

        private bool TryAdaptWithAdapters(TTarget target, out TTarget newTarget)
        {
            foreach (var adapter in adapters)
            {
                if (!adapter.CanAdapt(target)) continue;
                newTarget = adapter.Resolve(target);
                return true;
            }
            newTarget = default;
            return false;
        }
    }
}
