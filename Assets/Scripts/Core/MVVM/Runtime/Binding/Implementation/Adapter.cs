namespace Scaffold.MVVM.Binding
{
    public abstract class Adapter<TTarget>
    {
        public virtual bool CanAdapt(TTarget target)
        {
            return true;
        }

        public abstract TTarget Resolve(TTarget target);
    }
}
