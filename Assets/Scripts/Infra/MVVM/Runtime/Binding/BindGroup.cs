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

        public void Update()
        {
            if (contexts.Count == 0)
            {
                return;
            }
            foreach (var context in contexts)
{
    context.Update();
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







