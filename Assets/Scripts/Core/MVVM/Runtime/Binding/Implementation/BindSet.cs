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
                if (TryApplyConverter(converter, source, out target)) { return true; }
            }
            target = default;
            return false;
        }

        private bool TryApplyConverter(Converter<TSource, TTarget> converter, TSource source, out TTarget target)
        {
            if (!converter.CanConvert(source)) { target = default; return false; }
            target = converter.Convert(source);
            return true;
        }

        public bool TryAdapt(TTarget target, out TTarget newTarget)
        {
            foreach (var adapter in adapters)
            {
                if (TryApplyAdapter(adapter, target, out newTarget)) { return true; }
            }
            newTarget = default;
            return false;
        }

        private bool TryApplyAdapter(Adapter<TTarget> adapter, TTarget target, out TTarget newTarget)
        {
            if (!adapter.CanAdapt(target)) { newTarget = default; return false; }
            newTarget = adapter.Resolve(target);
            return true;
        }
    }
}
