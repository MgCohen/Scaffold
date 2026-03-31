namespace Scaffold.MVVM.Binding
{
    public interface IBindSet<TSource, TTarget> : IBindSet<TTarget>
    {
        public void RegisterConverter(Converter<TSource, TTarget> converter);
    }
}
