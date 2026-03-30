namespace Scaffold.MVVM.Binding
{
    public sealed class BindingOptions
    {
        public BindingOptions(bool lazy)
        {
            LazyEvaluation = lazy;
        }

        public bool LazyEvaluation { get; }

        public static readonly BindingOptions Strict = new BindingOptions(false);
        public static readonly BindingOptions Lazy = new BindingOptions(true);
    }
}

