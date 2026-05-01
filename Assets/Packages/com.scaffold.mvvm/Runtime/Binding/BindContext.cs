using System;
using System.Collections.Generic;
namespace Scaffold.MVVM.Binding
{
    public class BindContext<T> : IBindContext
    {
        public BindContext(Func<T> getter, IBindingDeferredCoordinator coordinator)
        {
            if (getter is null)
            {
                throw new ArgumentNullException(nameof(getter));
            }
            if (coordinator is null)
            {
                throw new ArgumentNullException(nameof(coordinator));
            }
            source = getter;
            this.coordinator = coordinator;
        }

        public bool IsEmpty => binds.Count == 0;

        private Func<T> source;
        private readonly IBindingDeferredCoordinator coordinator;
        private readonly List<BindRegistration> binds = new List<BindRegistration>();

        public void Bind(IBind<T> binding, BindingOptions options)
        {
            BindRegistration registration = CreateRegistration(binding, options);
            binds.Add(registration);
            ApplyInitialIfNeeded(registration);
        }

        public void OnBindingKeyChanged()
        {
            if (!TryGetValue(out T value))
            {
                return;
            }

            if (UpdateImmediateBinds(value))
            {
                coordinator.RequestDeferredFlush(this);
            }
        }

        private BindRegistration CreateRegistration(IBind<T> binding, BindingOptions options)
        {
            if (binding is null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            BindingUpdateTiming effectiveTiming = options.UpdateTiming ?? BindingUpdateTiming.Immediate;
            return new BindRegistration(binding, options, effectiveTiming);
        }

        private void ApplyInitialIfNeeded(BindRegistration registration)
        {
            if (registration.Options.LazyEvaluation)
            {
                return;
            }

            T value = source();
            registration.Bind.Update(value);
        }

        private bool UpdateImmediateBinds(T value)
        {
            bool anyDeferred = false;
            foreach (BindRegistration bind in binds)
            {
                if (bind.EffectiveTiming == BindingUpdateTiming.Immediate)
                {
                    bind.Bind.Update(value);
                }
                else
                {
                    anyDeferred = true;
                }
            }

            return anyDeferred;
        }

        public void FlushDeferredUpdates()
        {
            if (coordinator.IsUnbinding)
            {
                return;
            }
            if (!TryGetValue(out T value))
            {
                return;
            }
            foreach (BindRegistration bind in binds)
            {
                if (bind.EffectiveTiming != BindingUpdateTiming.Immediate)
                {
                    bind.Bind.Update(value);
                }
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
            public BindRegistration(IBind<T> bind, BindingOptions options, BindingUpdateTiming effectiveTiming)
            {
                if (bind is null)
                {
                    throw new ArgumentNullException(nameof(bind));
                }
                Bind = bind;
                Options = options;
                EffectiveTiming = effectiveTiming;
            }

            public IBind<T> Bind { get; }
            public BindingOptions Options { get; }
            public BindingUpdateTiming EffectiveTiming { get; }
        }
    }
}
