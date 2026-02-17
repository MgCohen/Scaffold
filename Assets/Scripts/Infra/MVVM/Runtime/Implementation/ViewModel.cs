using CommunityToolkit.Mvvm.ComponentModel;
using Scaffold.MVVM.Binding;
using Scaffold.Navigation;
using System.ComponentModel;

namespace Scaffold.MVVM
{
    [NestedObservableObject]
    public abstract partial class ViewModel : ObservableObject, IViewModel
    {
        protected INavigation navigation;
        protected IBindings binder = new TreeBinding();

        public void Bind(INavigation navigation)
        {
            binder.Unbind();

            this.navigation = navigation;
            this.Initialize();
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
            binder.UpdateBind(e.PropertyName);
            base.OnPropertyChanged(e);
        }

        public void Close()
        {
            navigation.Close(this);
            OnClosed();
        }

        protected virtual void OnClosed()
        {

        }
    }
}