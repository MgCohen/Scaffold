using System;
using System.Collections.Generic;

namespace Scaffold.MVVM.Binding
{
    public class BindContext<T>: IBindContext
    {
        public BindContext(Func<T> getter)
        {
            this.source = getter;
            this.currentValue = GetValue();
        }

        private Func<T> source;
        private List<IBind<T>> binds = new List<IBind<T>>();
        
        private T currentValue;

        public void Bind(IBind<T> binding)
        {
            binds.Add(binding);
            binding.Update(currentValue);
        }

        public void Update()
        {
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
            source = null;
            foreach(var setter in binds)
            {
                if(setter is IDisposable d)
                {
                    d.Dispose();
                }
            }
        }
    }
}