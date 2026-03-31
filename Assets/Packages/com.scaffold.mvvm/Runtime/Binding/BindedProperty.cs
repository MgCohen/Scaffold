using System;
using UnityEngine;
namespace Scaffold.MVVM.Binding
{
    internal class BindedProperty<TSource, TTarget> : IBind<TSource>, IBindedProperty<TSource, TTarget>
    {
        public BindedProperty(BindSet<TSource, TTarget> binding, Action<TTarget> setter, Action detach)
        {
            if (binding is null)
            {
                throw new ArgumentNullException(nameof(binding));
            }
            if (setter is null)
            {
                throw new ArgumentNullException(nameof(setter));
            }
            this.binding = binding;
            this.setter = setter;
            this.detach = detach;
        }

        private BindSet<TSource, TTarget> binding;
        private Action<TTarget> setter;
        private Action detach;
        private bool disposed;

        private Converter<TSource, TTarget> converter;
        private Adapter<TTarget> adapter;

        public void Update(TSource value)
        {
            if (setter == null) return;
            try { UpdateCore(value); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void UpdateCore(TSource value)
        {
            if (!TryConvertValue(value, out TTarget target))
            {
                throw new Exception($"No conversion method found for {typeof(TSource)} -> {typeof(TTarget)}");
            }
            if (adapter != null && adapter.CanAdapt(target))
            {
                target = adapter.Resolve(target);
            }
            setter(target);
        }

        private bool TryConvertValue(TSource sourceValue, out TTarget target)
        {
            if (converter != null && converter.CanConvert(sourceValue))
            {
                target = converter.Convert(sourceValue);
                return true;
            }
            if (binding.TryConvert(sourceValue, out target)) return true;
            return TryConvertFallback(sourceValue, out target);
        }

        private bool TryConvertFallback(TSource sourceValue, out TTarget target)
        {
            if (TryConvertString(sourceValue, out target)) return true;
            if (sourceValue is TTarget typedValue)
            {
                target = typedValue;
                return true;
            }
            bool canCastNull = sourceValue == null && typeof(TTarget) == typeof(TSource);
            target = canCastNull ? (TTarget)(object)sourceValue : default;
            return canCastNull;
        }

        private bool TryConvertString(TSource sourceValue, out TTarget target)
        {
            if (typeof(TTarget) == typeof(string) && sourceValue.ToString() is TTarget text)
            {
                target = text;
                return true;
            }
            target = default;
            return false;
        }

        public IBindedProperty<TSource, TTarget> WithConverter(Converter<TSource, TTarget> converter)
        {
            if (converter is null)
            {
                throw new ArgumentNullException(nameof(converter));
            }
            this.converter = converter;
            return this;
        }

        public IBindedProperty<TSource, TTarget> WithAdapter(Adapter<TTarget> adapter)
        {
            if (adapter is null)
            {
                throw new ArgumentNullException(nameof(adapter));
            }
            this.adapter = adapter;
            return this;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            detach?.Invoke();
            detach = null;
            setter = null;
        }
    }
}
