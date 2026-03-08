using Scaffold.MVVM.Binding;

namespace Scaffold.MVVM.Samples
{
    public class MVVMUseCases
    {
        public void UseCaseBindedPropertyCallback()
        {
            BindSet<int, string> bindSet = new BindSet<int, string>();
            string displayValue = null;
            BindedProperty<int, string> prop = new BindedProperty<int, string>(bindSet, v => displayValue = v);
            prop.Update(42);
        }

        public void UseCaseBindingPath()
        {
            BindingPath path = BindingPath.Create("viewModel.Player.Name");
            string top = path.Path;
            string child = path.Child.Path;
        }
    }
}
