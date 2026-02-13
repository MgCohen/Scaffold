using System;
using System.Linq.Expressions;

namespace Scaffold.MVVM.Binding
{
    internal class BindFactory
    {
        public BindFactory(BindSets sets)
        {
            this.sets = sets;
        }

        private BindSets sets;

        public BindedProperty<TSource, TTarget> CreateBind<TSource, TTarget>(Action<TTarget> target)
        {
            BindSet<TSource, TTarget> bindset = sets.GetSet<TSource, TTarget>();
            return new BindedProperty<TSource, TTarget>(bindset, target);
        }

        public BindedCollection<TSource, TTarget> CreateBind<TSource, TTarget>(ICollectionHandler<TSource, TTarget> handler)
        {
            BindSet<TSource, TTarget> bindset = sets.GetSet<TSource, TTarget>();
            return new BindedCollection<TSource, TTarget>(bindset, handler);
        }
    }
}