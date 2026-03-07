namespace Scaffold.MVVM.Binding
{
    public interface IBindSet
    {

    }

    public interface IBindSet<TTarget>: IBindSet
    {
        public void RegisterAdapter(Adapter<TTarget> adapter);
    }

    public interface IBindSet<TSource, TTarget>: IBindSet<TTarget>
    {
        public void RegisterConverter(Converter<TSource, TTarget> converter);
    }
}
