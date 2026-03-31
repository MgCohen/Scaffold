namespace Scaffold.MVVM.Binding
{
    public interface ICollectionHandler<TSource, TTarget>
    {
        public TTarget Add(TSource source);
        public void Remove(TTarget item);
    }
}



