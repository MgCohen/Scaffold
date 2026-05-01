namespace Scaffold.MVVM.Binding
{
    public sealed class BindingOptions
    {
        public BindingOptions(bool lazyEvaluation, BindingUpdateTiming? updateTiming = null)
        {
            LazyEvaluation = lazyEvaluation;
            UpdateTiming = updateTiming;
        }

        public bool LazyEvaluation { get; }

        public BindingUpdateTiming? UpdateTiming { get; }

        public static readonly BindingOptions Strict = new BindingOptions(false);

        public static readonly BindingOptions Lazy = new BindingOptions(true);

        public static readonly BindingOptions StrictImmediate = new BindingOptions(false, BindingUpdateTiming.Immediate);
    }
}
