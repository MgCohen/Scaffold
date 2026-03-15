using System;
using System.Collections.Generic;

namespace Scaffold.MVVM.Binding
{
    public class BindContext<T>: IBindContext
    {
        public BindContext(Func<T> getter)
        {
            if (getter is null) { throw new ArgumentNullException(nameof(getter)); }
            this.source = getter;
            this.currentValue = GetValue();
        }

        private Func<T> source;
        private List<IBind<T>> binds = new List<IBind<T>>();

        private T currentValue;

        public void Bind(IBind<T> binding)
        {
            if (binding is null) { throw new ArgumentNullException(nameof(binding)); }
            binds.Add(binding);
            binding.Update(currentValue);
        }

        public void Update()
        {
            if (source == null) { return; }
            T value = GetValue();
            UpdateSetters(value);
        }

        private T GetValue()
        {
            return source();
        }

        private void UpdateSetters(T value)
        {
            foreach (var setter in binds)
            {
                Set(setter, value);
            }
        }

        private void Set(IBind<T> binding, T value)
        {
            binding.Update(value);
        }

        public void Unbind()
        {
            if (!CanUnbind()) { return; }
            source = null;
            DisposeBinds();
        }

        private bool CanUnbind()
        {
            return source != null || binds.Count > 0;
        }

        private void DisposeBinds()
        {
            foreach (var setter in binds)
            {
                if (setter is IDisposable disposable) { disposable.Dispose(); }
            }
        }
    }
}
