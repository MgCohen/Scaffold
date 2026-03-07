using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq.Expressions;
using UnityEngine;

namespace Scaffold.MVVM.Binding
{
    public class BindSet<TSource, TTarget> : IBindSet<TSource, TTarget>
    {
        private readonly List<Converter<TSource, TTarget>> converters = new List<Converter<TSource, TTarget>>();
        private readonly List<Adapter<TTarget>> adapters = new List<Adapter<TTarget>>();

        public void RegisterConverter(Converter<TSource, TTarget> converter)
        {
            converters.Add(converter);
        }

        public void RegisterAdapter(Adapter<TTarget> adapter)
        {
            adapters.Add(adapter);
        }

        public bool TryConvert(TSource source, out TTarget target)
        {
            foreach (var converter in converters)
            {
                if (converter.CanConvert(source))
                {
                    target = converter.Convert(source);
                    return true;
                }
            }
            target = default;
            return false;
        }

        public bool TryAdapt(TTarget target, out TTarget newTarget)
        {
            foreach(var adapter in adapters)
            {
                if (adapter.CanAdapt(target))
                {
                    newTarget = adapter.Resolve(target);
                    return true;
                }
            }
            newTarget = default;
            return false;
        }
    }
}
