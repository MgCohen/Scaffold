namespace Scaffold.MVVM.Binding
{
    public class BindingOptions
    {
        public static readonly BindingOptions Strict = new BindingOptions(false);
        public static readonly BindingOptions Lazy = new BindingOptions(true);

        public BindingOptions(bool lazy)
        {
            LazyEvaluation = lazy;
        }

        public bool LazyEvaluation { get; }
    }
}
