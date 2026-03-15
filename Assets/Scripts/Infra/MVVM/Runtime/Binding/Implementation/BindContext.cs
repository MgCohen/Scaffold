using System;
using System.Collections.Generic;

namespace Scaffold.MVVM.Binding
{
    public class BindContext<T> : IBindContext
    {
        private sealed class BindRegistration
        {
            public BindRegistration(IBind<T> bind, BindingOptions options)
            {
                Bind = bind;
                Options = options ?? BindingOptions.Strict;
            }

            public IBind<T> Bind { get; }
            public BindingOptions Options { get; }
        }

        public BindContext(Func<T> getter)
        {
            if (getter is null) { throw new ArgumentNullException(nameof(getter)); }
            source = getter;
        }

        private Func<T> source;
        private readonly List<BindRegistration> binds = new List<BindRegistration>();

        public bool IsEmpty => binds.Count == 0;

        public void Bind(IBind<T> binding, BindingOptions options)
        {
            if (binding is null) { throw new ArgumentNullException(nameof(binding)); }
            BindRegistration registration = new BindRegistration(binding, options);
            binds.Add(registration);

            if (registration.Options.LazyEvaluation)
            {
                return;
            }

            T value = GetValue();
            binding.Update(value);
        }

        public void Update()
        {
            if (source == null || binds.Count == 0) { return; }

            T value;
            try
            {
                value = GetValue();
            }
            catch (NullReferenceException)
            {
                if (HasStrictBind())
                {
                    throw;
                }

                return;
            }

            foreach (BindRegistration bind in binds)
            {
                bind.Bind.Update(value);
            }
        }

        public void Unbind(IBind<T> binding)
        {
            if (binding is null) { throw new ArgumentNullException(nameof(binding)); }
            for (int i = binds.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(binds[i].Bind, binding))
                {
                    binds.RemoveAt(i);
                    break;
                }
            }
        }

        private bool HasStrictBind()
        {
            foreach (BindRegistration bind in binds)
            {
                if (bind.Options.LazyEvaluation == false)
                {
                    return true;
                }
            }

            return false;
        }

        private T GetValue()
        {
            return source();
        }

        public void Unbind()
        {
            if (!CanUnbind()) { return; }
            source = null;
            DisposeBinds();
            binds.Clear();
        }

        private bool CanUnbind()
        {
            return source != null || binds.Count > 0;
        }

        private void DisposeBinds()
        {
            foreach (BindRegistration bind in binds)
            {
                if (bind.Bind is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
