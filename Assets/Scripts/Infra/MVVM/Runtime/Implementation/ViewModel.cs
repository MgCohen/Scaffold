using CommunityToolkit.Mvvm.ComponentModel;
using Scaffold.MVVM.Binding;
using Scaffold.Navigation;
using System.ComponentModel;

namespace Scaffold.MVVM
{
    [NestedObservableObject]
    [BindSource(typeof(TreeBinding))]
    public abstract partial class ViewModel : ObservableObject, IViewModel
    {
        protected INavigation navigation;

        public void Bind(INavigation navigation)
        {
            GuardBindingState();
            this.navigation = navigation;
            Initialize();
        }

        private void GuardBindingState()
        {
            ClearBindings();
        }

        protected T BindChildViewModel<T>(T viewModel) where T: IViewModel
        {
            viewModel.Bind(navigation);
            return viewModel;
        }

        protected virtual void Initialize()
        {

        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            UpdateBinding(e.PropertyName);
            base.OnPropertyChanged(e);
        }

        public void Close()
        {
            if (navigation == null) { return; }
            navigation.Close(this);
            OnClosed();
        }

        protected virtual void OnClosed()
        {

        }
    }
}
