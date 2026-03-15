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

        public BindedProperty<TSource, TTarget> CreateBind<TSource, TTarget>(Action<TTarget> target, Action detach)
        {
            if (target is null) { throw new ArgumentNullException(nameof(target)); }
            BindSet<TSource, TTarget> bindset = sets.GetSet<TSource, TTarget>();
            return new BindedProperty<TSource, TTarget>(bindset, target, detach);
        }

        public BindedCollection<TSource, TTarget> CreateBind<TSource, TTarget>(ICollectionHandler<TSource, TTarget> handler, Action detach)
        {
            if (handler is null) { throw new ArgumentNullException(nameof(handler)); }
            BindSet<TSource, TTarget> bindset = sets.GetSet<TSource, TTarget>();
            return new BindedCollection<TSource, TTarget>(bindset, handler, detach);
        }
    }
}


