namespace Scaffold.MVVM.Binding
{
    public abstract class Converter<TSource, TTarget>
    {
        public virtual bool CanConvert(TSource source)
        {
            if (source is null)
            {
                return false;
            }
            return true;
        }

        public abstract TTarget Convert(TSource source);
    }
}

