using System;
using System.Collections.Generic;
namespace Scaffold.MVVM.Binding
{
    public class BindContext<T> : IBindContext
    {
        public BindContext(Func<T> getter)
        {
            if (getter is null)
{
    throw new ArgumentNullException(nameof(getter));
}
            source = getter;
        }

        public bool IsEmpty => binds.Count == 0;

        private Func<T> source;
        private readonly List<BindRegistration> binds = new List<BindRegistration>();

        public void Bind(IBind<T> binding, BindingOptions options)
        {
            if (binding is null)
{
    throw new ArgumentNullException(nameof(binding));
}
            BindRegistration registration = new BindRegistration(binding, options);
            binds.Add(registration);
            if (registration.Options.LazyEvaluation)
{
    return;
}
            T value = source();
            binding.Update(value);
        }

        public void Update()
        {
            if (!TryGetValue(out T value))
{
    return;
}
            foreach (BindRegistration bind in binds)
{
    bind.Bind.Update(value);
}
        }

        private bool TryGetValue(out T value)
        {
            if (source == null || binds.Count == 0)
            {
                value = default;
                return false;
            }
            try { value = source(); return true; }
            catch (NullReferenceException ex)
            {
                if (HasStrictBind()) throw ex;
                value = default;
                return false;
            }
        }

        private bool HasStrictBind()
        {
            return binds.Exists(bind => bind.Options.LazyEvaluation == false);
        }

        public void Unbind(IBind<T> binding)
        {
            if (binding is null)
{
    throw new ArgumentNullException(nameof(binding));
}
            for (int i = binds.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(binds[i].Bind, binding))
                {
                    binds.RemoveAt(i);
                    break;
                }
            }
        }

        public void Unbind()
        {
            if (source == null && binds.Count == 0)
{
    return;
}
            source = null;
            DisposeBinds();
            binds.Clear();
        }

        private void DisposeBinds()
        {
            foreach (BindRegistration bind in binds)
            {
                if (bind.Bind is IDisposable disposable) disposable.Dispose();
            }
        }

        private sealed class BindRegistration
        {
            public BindRegistration(IBind<T> bind, BindingOptions options)
            {
                if (bind is null)
{
    throw new ArgumentNullException(nameof(bind));
}
                Bind = bind;
                Options = options ?? BindingOptions.Strict;
            }

            public IBind<T> Bind { get; }
            public BindingOptions Options { get; }
        }
    }
}
