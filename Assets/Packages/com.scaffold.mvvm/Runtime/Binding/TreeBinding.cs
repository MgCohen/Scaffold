using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;
namespace Scaffold.MVVM.Binding
{
    public class TreeBinding : IBindings
    {
        public TreeBinding()
        {
            bindSets = new BindSets();
            groups = new BindGroups();
            factory = new BindFactory(bindSets);
            registry = new BindRegistry(groups);
        }

        private readonly BindSets bindSets;
        private readonly BindGroups groups;
        private readonly BindFactory factory;
        private readonly BindRegistry registry;
        private bool isUnbinding;

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
            registration.Context.Bind(bindedProp, options ?? BindingOptions.Strict);
            return bindedProp;
        }

        public IBindedCollection<TSource, TTarget> RegisterBindCollection<TSource, TTarget>(Expression<Func<ICollection<TSource>>> source, ICollectionHandler<TSource, TTarget> handler, BindingOptions options = null)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (handler is null) throw new ArgumentNullException(nameof(handler));
            RegistrationContext<ICollection<TSource>> registration = registry.GetOrCreateContext(source);
            BindedCollection<TSource, TTarget> bindedProp = null;
            bindedProp = factory.CreateBind<TSource, TTarget>(handler, () => DetachBind(registration, bindedProp));
            registration.Context.Bind(bindedProp, options ?? BindingOptions.Strict);
            return bindedProp;
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
            Debug.Log(bindKey);
            BindGroup group = groups.GetGroup(bindKey);
            group.Update();
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
                bindSets.Clear();
                groups.Clear();
                registry.Clear();
            }
            finally { isUnbinding = false; }
        }
    }
}
