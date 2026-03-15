using System;
using System.Collections.Generic;

namespace Scaffold.MVVM.Binding
{
    internal class BindGroup
    {
        private List<IBindContext> contexts = new List<IBindContext>();

        public void Bind(IBindContext context)
        {
            if (context is null) { throw new ArgumentNullException(nameof(context)); }
            contexts.Add(context);
        }

        public void Update()
        {
            if (contexts.Count == 0) { return; }
            foreach(var context in contexts)
            {
                context.Update();
            }
        }
    }
}


