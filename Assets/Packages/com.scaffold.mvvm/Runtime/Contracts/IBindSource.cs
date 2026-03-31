using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Scaffold.MVVM.Binding
{
    public interface IBindSource
    {
        public IBindedProperty<TSource, TTarget> Bind<TSource, TTarget>(Expression<Func<TSource>> source, Expression<Func<TTarget>> target, BindingOptions options = null);
        public IBindedProperty<TSource, TTarget> Bind<TSource, TTarget>(Expression<Func<TSource>> source, Action<TTarget> target, BindingOptions options = null);
        public void BindConverter<TSource, TTarget>(Func<TSource, TTarget> converter);
        public void BindConverter<TSource, TTarget>(Converter<TSource, TTarget> converter);
        public IBindedCollection<TSource, TTarget> BindCollection<TSource, TTarget>(Expression<Func<ICollection<TSource>>> source, ICollectionHandler<TSource, TTarget> handler, BindingOptions options = null);
        public void UpdateBinding(string bindKey);
        public void ClearBindings();
    }
}



