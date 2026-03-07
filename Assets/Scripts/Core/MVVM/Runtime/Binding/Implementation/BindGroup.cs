using System.Collections.Generic;

namespace Scaffold.MVVM.Binding
{
    internal class BindGroup
    {
        private List<IBindContext> contexts = new List<IBindContext>();

        public void Bind(IBindContext context)
        {
            contexts.Add(context);
        }

        public void Update()
        {
            foreach(var context in contexts)
            {
                context.Update();
            }
        }
    }
}
