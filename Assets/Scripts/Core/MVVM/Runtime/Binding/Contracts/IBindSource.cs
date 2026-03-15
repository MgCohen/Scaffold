using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Scaffold.MVVM.Binding.Contracts
{
}

namespace Scaffold.MVVM.Binding
{
    public interface IBindSource
    {
        public IBindedProperty<TSource, TTarget> Bind<TSource, TTarget>(Expression<Func<TSource>> source, Expression<Func<TTarget>> target);
        public IBindedProperty<TSource, TTarget> Bind<TSource, TTarget>(Expression<Func<TSource>> source, Action<TTarget> target);
        public void BindConverter<TSource, TTarget>(Func<TSource, TTarget> converter);
        public void BindConverter<TSource, TTarget>(Converter<TSource, TTarget> converter);
        public void BindCollection<TSource, TTarget>(Expression<Func<ICollection<TSource>>> source, ICollectionHandler<TSource, TTarget> handler);
        public void UpdateBinding(string bindKey);
        public void ClearBindings();
    }
}
