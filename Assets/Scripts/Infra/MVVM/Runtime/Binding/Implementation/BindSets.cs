using Mono.Cecil;
using Scaffold.Maps;
using System;

namespace Scaffold.MVVM.Binding
{
    internal class BindSets
    {
        private Map<Type, Type, IBindSet> sets = new Map<Type, Type, IBindSet>();

        internal void RegisterAdapter<TTarget>(Adapter<TTarget> adapter)
        {
            //TODO
            //BindSet<TTarget> bindset = GetSet<TTarget>();
            //bindset.RegisterAdapter(adapter);
        }

        internal void RegisterConverter<TSource, TTarget>(Converter<TSource, TTarget> converter)
        {
            IBindSet<TSource, TTarget> bindset = GetSet<TSource, TTarget>();
            bindset.RegisterConverter(converter);
        }

        internal BindSet<TSource, TTarget> GetSet<TSource, TTarget>()
        {
            Type sType = typeof(TSource);
            Type tType = typeof(TTarget);
            if(!sets.TryGetValue(sType, tType, out IBindSet set))
            {
                set = new BindSet<TSource, TTarget>();
                sets.Add(sType, tType, set);
            }
            return set as BindSet<TSource, TTarget>;
        }

        internal void Clear()
        {
            sets.Clear();
        }

    }
}