using System;

namespace Scaffold.MVVM.Binding
{
    public interface IBindedProperty<TSource, TTarget>: IDisposable
    {
        public IBindedProperty<TSource, TTarget> WithConverter(Converter<TSource, TTarget> converter);

        public IBindedProperty<TSource, TTarget> WithAdapter(Adapter<TTarget> converter);
    }
}



