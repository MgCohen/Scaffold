using System;
using System.Linq.Expressions;

namespace Scaffold.MVVM.Binding
{
    internal class BindFactory
    {
        public BindFactory(BindSets sets)
        {
            if (sets is null) { throw new ArgumentNullException(nameof(sets)); }
            this.sets = sets;
        }

        private BindSets sets;

        public BindedProperty<TSource, TTarget> CreateBind<TSource, TTarget>(Action<TTarget> target)
        {
            if (target is null) { throw new ArgumentNullException(nameof(target)); }
            BindSet<TSource, TTarget> bindset = sets.GetSet<TSource, TTarget>();
            return new BindedProperty<TSource, TTarget>(bindset, target);
        }

        public BindedCollection<TSource, TTarget> CreateBind<TSource, TTarget>(ICollectionHandler<TSource, TTarget> handler)
        {
            if (handler is null) { throw new ArgumentNullException(nameof(handler)); }
            BindSet<TSource, TTarget> bindset = sets.GetSet<TSource, TTarget>();
            return new BindedCollection<TSource, TTarget>(bindset, handler);
        }
    }
}


