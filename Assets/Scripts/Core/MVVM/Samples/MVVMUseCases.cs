using CommunityToolkit.Mvvm.ComponentModel;
using Scaffold.MVVM.Binding;
using UnityEngine;

namespace Scaffold.MVVM.Samples
{
    public class MVVMUseCases
    {
        public void UseCaseBindModelToViewModel()
        {
            SampleViewModel viewModel = new SampleViewModel();
            viewModel.Bind(null);
            RegisterNested(viewModel);
            viewModel.SampleModel.Value = 7;
            Debug.Log($"Model value mirrored into ViewModel: {viewModel.Value}");
        }

        public void UseCaseBindViewToViewModel()
        {
            SampleViewModel viewModel = new SampleViewModel();
            viewModel.Bind(null);
            RegisterNested(viewModel);
            GameObject host = new GameObject(nameof(SampleView));
            SampleView view = host.AddComponent<SampleView>();
            view.Bind(viewModel);
            viewModel.SampleModel.Value = 9;
            Debug.Log($"View rendered value: {view.LastValue}");
            Object.Destroy(host);
        }

        private static void RegisterNested(INestedObservableProperties nested)
        {
            nested.RegisterNestedProperties();
        }
    }

    public partial class SampleModel : Model
    {
        [ObservableProperty]
        private int value;
    }

    public partial class SampleViewModel : ViewModel
    {
        [ObservableProperty]
        private SampleModel sampleModel = new SampleModel();

        [ObservableProperty]
        private int value;

        protected override void Initialize()
        {
            Bind(() => SampleModel.Value, () => Value);
            Value = SampleModel.Value;
        }
    }

    public class SampleView : View<SampleViewModel>
    {
        public int LastValue { get; private set; }

        protected override void OnBind()
        {
            Bind<int, int>(() => viewModel.Value, OnValueChanged);
        }

        private void OnValueChanged(int value)
        {
            LastValue = value;
            Debug.Log($"SampleView value changed: {value}");
        }
    }
}
