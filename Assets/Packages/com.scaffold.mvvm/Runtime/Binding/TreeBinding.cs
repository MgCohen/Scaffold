using System;
using System.Collections.Generic;
using System.Linq.Expressions;
namespace Scaffold.MVVM.Binding
{
    public class TreeBinding : IBindings, IBindingDeferredCoordinator
    {
        public TreeBinding()
        {
            bindSets = new BindSets();
            groups = new BindGroups();
            factory = new BindFactory(bindSets);
            registry = new BindRegistry(groups, this);
        }

        bool IBindingDeferredCoordinator.IsUnbinding => isUnbinding;

        private readonly BindSets bindSets;
        private readonly BindGroups groups;
        private readonly BindFactory factory;
        private readonly BindRegistry registry;
        private bool isUnbinding;

        private BindingUpdateTiming defaultBindingUpdateTiming = BindingUpdateTiming.Immediate;
        private IDeferredBindingScheduler deferredScheduler;
        private readonly HashSet<IBindContext> deferredDirtyContexts = new HashSet<IBindContext>();
        private bool flushScheduled;

        public void RegisterBindingUpdatePolicy(BindingUpdateTiming timing, IDeferredBindingScheduler scheduler)
        {
            if (timing != BindingUpdateTiming.Immediate && scheduler == null)
            {
                throw new ArgumentNullException(nameof(scheduler), "Deferred binding update timing requires a non-null scheduler.");
            }
            defaultBindingUpdateTiming = timing;
            deferredScheduler = scheduler;
        }

        public IBindedProperty<TSource, TTarget> RegisterBind<TSource, TTarget>(Expression<Func<TSource>> source, Expression<Func<TTarget>> target, BindingOptions options = null)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            Action<TTarget> targetSetter = target.CreateSetter().Compile();
            return RegisterBind(source, targetSetter, options);
        }

        public IBindedProperty<TSource, TTarget> RegisterBind<TSource, TTarget>(Expression<Func<TSource>> source, Action<TTarget> target, BindingOptions options = null)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (target is null) throw new ArgumentNullException(nameof(target));
            RegistrationContext<TSource> registration = registry.GetOrCreateContext(source);
            BindedProperty<TSource, TTarget> bindedProp = null;
            bindedProp = factory.CreateBind<TSource, TTarget>(target, () => DetachBind(registration, bindedProp));
            BindingOptions resolved = ResolveBindingOptions(options);
            registration.Context.Bind(bindedProp, resolved);
            return bindedProp;
        }

        public IBindedCollection<TSource, TTarget> RegisterBindCollection<TSource, TTarget>(Expression<Func<ICollection<TSource>>> source, ICollectionHandler<TSource, TTarget> handler, BindingOptions options = null)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (handler is null) throw new ArgumentNullException(nameof(handler));
            RegistrationContext<ICollection<TSource>> registration = registry.GetOrCreateContext(source);
            BindedCollection<TSource, TTarget> bindedProp = null;
            bindedProp = factory.CreateBind<TSource, TTarget>(handler, () => DetachBind(registration, bindedProp));
            BindingOptions resolved = ResolveBindingOptions(options);
            registration.Context.Bind(bindedProp, resolved);
            return bindedProp;
        }

        private BindingOptions ResolveBindingOptions(BindingOptions options)
        {
            BindingOptions o = options ?? BindingOptions.Strict;
            BindingUpdateTiming effective = o.UpdateTiming ?? defaultBindingUpdateTiming;
            if (effective != BindingUpdateTiming.Immediate && deferredScheduler == null)
            {
                throw new InvalidOperationException(
                    "Deferred binding updates require a scheduler. Call RegisterBindingUpdatePolicy with a non-null IDeferredBindingScheduler, or use BindingUpdateTiming.Immediate.");
            }
            return new BindingOptions(o.LazyEvaluation, effective);
        }

        private void DetachBind<TSource>(RegistrationContext<TSource> registration, IBind<TSource> bind)
        {
            if (isUnbinding)
            {
                return;
            }
            registration.Context.Unbind(bind);
            registry.RemoveIfEmpty(registration.Path, registration.SourceType, registration.Context);
        }

        public void UpdateBind(string bindKey)
        {
            if (string.IsNullOrWhiteSpace(bindKey))
            {
                return;
            }
            BindGroup group = groups.GetGroup(bindKey);
            group.NotifyBindingKeyChanged();
        }

        public void RegisterConverter<TSource, TTarget>(Converter<TSource, TTarget> converter)
        {
            if (converter is null)
            {
                throw new ArgumentNullException(nameof(converter));
            }
            bindSets.RegisterConverter<TSource, TTarget>(converter);
        }

        public void RegisterAdapter<TTarget>(Adapter<TTarget> adapter)
        {
            if (adapter is null)
            {
                throw new ArgumentNullException(nameof(adapter));
            }
            bindSets.RegisterAdapter<TTarget>(adapter);
        }

        public void Unbind()
        {
            if (bindSets == null || groups == null || registry == null)
            {
                return;
            }
            isUnbinding = true;
            try
            {
                deferredDirtyContexts.Clear();
                flushScheduled = false;
                bindSets.Clear();
                groups.Clear();
                registry.Clear();
            }
            finally { isUnbinding = false; }
        }

        void IBindingDeferredCoordinator.RequestDeferredFlush(IBindContext context)
        {
            if (isUnbinding)
            {
                return;
            }

            deferredDirtyContexts.Add(context);
            if (flushScheduled)
            {
                return;
            }

            if (deferredScheduler == null)
            {
                return;
            }

            flushScheduled = true;
            deferredScheduler.Schedule(FlushDeferredWork);
        }

        private void FlushDeferredWork()
        {
            if (isUnbinding)
            {
                ResetDeferredFlushState();
                return;
            }

            flushScheduled = false;
            FlushPendingDeferredContexts();
            RescheduleDeferredFlushIfNeeded();
        }

        private void ResetDeferredFlushState()
        {
            flushScheduled = false;
            deferredDirtyContexts.Clear();
        }

        private void FlushPendingDeferredContexts()
        {
            IBindContext[] snapshot = new IBindContext[deferredDirtyContexts.Count];
            deferredDirtyContexts.CopyTo(snapshot);
            deferredDirtyContexts.Clear();
            foreach (IBindContext ctx in snapshot)
            {
                ctx.FlushDeferredUpdates();
            }
        }

        private void RescheduleDeferredFlushIfNeeded()
        {
            if (deferredDirtyContexts.Count > 0 && deferredScheduler != null)
            {
                flushScheduled = true;
                deferredScheduler.Schedule(FlushDeferredWork);
            }
        }
    }
}
