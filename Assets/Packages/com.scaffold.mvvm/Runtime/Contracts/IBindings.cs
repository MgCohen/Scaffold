using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Scaffold.MVVM.Binding
{
    public interface IBindings
    {
        public void UpdateBind(string bindKey);
        public IBindedProperty<TSource,TTarget> RegisterBind<TSource, TTarget>(Expression<Func<TSource>> source, Expression<Func<TTarget>> target, BindingOptions options = null);
        public IBindedProperty<TSource, TTarget> RegisterBind<TSource, TTarget>(Expression<Func<TSource>> source, Action<TTarget> target, BindingOptions options = null);
        public IBindedCollection<TSource, TTarget> RegisterBindCollection<TSource, TTarget>(Expression<Func<ICollection<TSource>>> source, ICollectionHandler<TSource, TTarget> handler, BindingOptions options = null);
        public void RegisterConverter<Tsource, TTarget>(Converter<Tsource, TTarget> converter);
        public void RegisterAdapter<TTarget>(Adapter<TTarget> converter);
        public void Unbind();
    }
}



