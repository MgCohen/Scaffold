using System;
using Scaffold.MVVM.Contracts;
using Scaffold.Navigation.Contracts;

namespace Scaffold.MVVM
{
    public abstract class ViewElement<T, J> : ViewElement<J> where T : ViewElement where J : IViewModel
    {
        protected T parent;

        public void Bind(T parent, J viewModel)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }
            if (viewModel == null)
            {
                return;
            }
            this.parent = parent;
            Bind(viewModel);
        }
    }
}
