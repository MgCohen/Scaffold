namespace Scaffold.MVVM.Binding
{
    public interface IBindSet<TTarget> : IBindSet
    {
        public void RegisterAdapter(Adapter<TTarget> adapter);
    }
}
