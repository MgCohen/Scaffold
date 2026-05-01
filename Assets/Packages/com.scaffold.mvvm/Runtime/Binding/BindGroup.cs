using System;
using System.Collections.Generic;
namespace Scaffold.MVVM.Binding
{
    internal class BindGroup
    {
        public bool IsEmpty => contexts.Count == 0;
        private readonly List<IBindContext> contexts = new List<IBindContext>();

        public void Bind(IBindContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            contexts.Add(context);
        }

        public void NotifyBindingKeyChanged()
        {
            if (contexts.Count == 0)
            {
                return;
            }
            foreach (IBindContext context in contexts)
            {
                context.OnBindingKeyChanged();
            }
        }

        public void Unbind(IBindContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            contexts.Remove(context);
        }
    }
}
