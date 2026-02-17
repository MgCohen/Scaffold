using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;

namespace Scaffold.MVVM.Binding
{
    public class TreeBinding : IBindings
    {
        private BindSets bindSets;
        private BindGroups groups;
        private BindFactory factory;
        private BindRegistry registry;

        public TreeBinding()
        {
            bindSets = new BindSets();
            groups = new BindGroups();
            factory = new BindFactory(bindSets);
            registry = new BindRegistry(groups);
        }

        public IBindedProperty<TSource, TTarget> RegisterBind<TSource, TTarget>(Expression<Func<TSource>> source, Expression<Func<TTarget>> target)
        {
            Action<TTarget> targetSetter = target.CreateSetter().Compile();
            return RegisterBind(source, targetSetter);
        }

        public IBindedProperty<TSource, TTarget> RegisterBind<TSource, TTarget>(Expression<Func<TSource>> source, Action<TTarget> target)
        {
            BindedProperty<TSource, TTarget> bindedProp = factory.CreateBind<TSource, TTarget>(target);
            RegisterBind(source, bindedProp);
            return bindedProp;
        }

        public IBindedCollection<TSource, TTarget> RegisterBindCollection<TSource, TTarget>(Expression<Func<ICollection<TSource>>> source, ICollectionHandler<TSource, TTarget> handler)
        {
            BindedCollection<TSource, TTarget> bindedProp = factory.CreateBind<TSource, TTarget>(handler);
            RegisterBind(source, bindedProp);
            return bindedProp;
        }

        private void RegisterBind<TSource>(Expression<Func<TSource>> source, IBind<TSource> bindedProp)
        {
            BindContext<TSource> context = registry.GetContext(source);
            context.Bind(bindedProp);
        }

        public void UpdateBind(string bindKey)
        {
            Debug.Log(bindKey);
            BindGroup group = groups.GetGroup(bindKey);
            group.Update();
        }

        public void RegisterConverter<TSource, TTarget>(Converter<TSource, TTarget> converter)
        {
            bindSets.RegisterConverter<TSource, TTarget>(converter);
        }

        public void RegisterAdapter<TTarget>(Adapter<TTarget> adapter)
        {
            bindSets.RegisterAdapter<TTarget>(adapter);
        }
        public void Unbind()
        {
            bindSets.Clear();
            groups.Clear();
            registry.Clear();
        }
    }
}