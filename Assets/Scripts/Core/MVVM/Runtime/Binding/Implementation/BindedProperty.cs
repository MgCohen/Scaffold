using System;
using UnityEngine;

namespace Scaffold.MVVM.Binding
{
    internal class BindedProperty<TSource, TTarget> : IBind<TSource>, IBindedProperty<TSource, TTarget>
    {
        public BindedProperty(BindSet<TSource, TTarget> binding, Action<TTarget> setter)
        {
            if (binding is null) { throw new ArgumentNullException(nameof(binding)); }
            if (setter is null) { throw new ArgumentNullException(nameof(setter)); }
            this.binding = binding;
            this.setter = setter;
        }

        private BindSet<TSource, TTarget> binding;
        private Action<TTarget> setter;

        private Converter<TSource, TTarget> converter;
        private Adapter<TTarget> adapter;

        public void Update(TSource value)
        {
            if (setter == null) { return; }
            try { ApplyValue(value); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void ApplyValue(TSource value)
        {
            var targetValue = ResolveValue(value);
            setter(targetValue);
        }

        public IBindedProperty<TSource, TTarget> WithConverter(Converter<TSource, TTarget> converter)
        {
            if (converter is null) { throw new ArgumentNullException(nameof(converter)); }
            this.converter = converter;
            return this;
        }

        public IBindedProperty<TSource, TTarget> WithAdapter(Adapter<TTarget> adapter)
        {
            if (adapter is null) { throw new ArgumentNullException(nameof(adapter)); }
            this.adapter = adapter;
            return this;
        }

        private TTarget ResolveValue(TSource sourceValue)
        {
            var converted = Convert(sourceValue);
            return Adapt(converted);
        }

        private TTarget Convert(TSource sourceValue)
        {
            if (sourceValue is TTarget tv) { return tv; }
            if (sourceValue == null && typeof(TTarget) == typeof(TSource)) { return (TTarget)(object)sourceValue; }
            if (converter != null && converter.CanConvert(sourceValue)) { return converter.Convert(sourceValue); }
            if (binding.TryConvert(sourceValue, out TTarget cv)) { return cv; }
            if (typeof(TTarget) == typeof(string) && sourceValue.ToString() is TTarget tt) { return tt; }
            throw new Exception($"No conversion method found for {typeof(TSource)} -> {typeof(TTarget)}");
        }

        private TTarget Adapt(TTarget target)
        {
            if (adapter != null && adapter.CanAdapt(target))
            {
                return adapter.Resolve(target);
            }
            return target;
        }
    }
}


