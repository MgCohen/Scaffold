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

        /// <summary>
        /// When null, the bind uses the bind-source default from <see cref="IBindings.RegisterBindingUpdatePolicy"/>.
        /// After resolution at registration time, implementations store a non-null effective value on the bind.
        /// </summary>
        public BindingUpdateTiming? UpdateTiming { get; }

        public static readonly BindingOptions Strict = new BindingOptions(false);

        public static readonly BindingOptions Lazy = new BindingOptions(true);

        /// <summary>
        /// Strict initial bind + immediate updates (explicit override; does not inherit bind-source timing).
        /// </summary>
        public static readonly BindingOptions StrictImmediate = new BindingOptions(false, BindingUpdateTiming.Immediate);
    }
}
