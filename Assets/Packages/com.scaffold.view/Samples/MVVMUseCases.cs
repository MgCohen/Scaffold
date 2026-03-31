using Scaffold.Navigation.Contracts;
using Scaffold.MVVM.Contracts;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Scaffold.MVVM.Binding;
namespace Scaffold.MVVM.Samples
{
    public class MVVMUseCases
    {
        public void UseCaseBindModelToViewModel()
        {
            SampleViewModel viewModel = new SampleViewModel();
            viewModel.Bind(null);
            BuildRegisterNested(viewModel);
            viewModel.SampleModel.Value = 7;
            Debug.Log($"Model value mirrored into ViewModel: {viewModel.Value}");
        }

        public void UseCaseBindViewToViewModel()
        {
            SampleViewModel viewModel = new SampleViewModel();
            viewModel.Bind(null);
            BuildRegisterNested(viewModel);
            using SampleViewHost host = CreateAndBindView(viewModel);
            viewModel.SampleModel.Value = 9;
            Debug.Log($"View rendered value: {host.View.LastValue}");
        }

        private static void BuildRegisterNested(INestedObservableProperties nested)
        {
            if (nested == null)
            {
                return;
            }
            nested.RegisterNestedProperties();
        }

        private static SampleViewHost CreateAndBindView(SampleViewModel viewModel)
        {
            GameObject host = new GameObject(nameof(SampleView));
            SampleView view = host.AddComponent<SampleView>();
            view.Bind(viewModel);
            return new SampleViewHost(host, view);
        }

        private sealed class SampleViewHost : System.IDisposable
        {
            public SampleViewHost(GameObject host, SampleView view)
            {
                this.host = host;
                View = view;
            }

            public SampleView View { get; }
            private readonly GameObject host;

            public void Dispose()
            {
                UnityEngine.Object.Destroy(host);
            }
        }
    }

    public partial class BuildSampleModel : Model
    {
        [ObservableProperty]
        private int value;
    }

    public partial class SampleViewModel : ViewModel
    {
        [ObservableProperty]
        private BuildSampleModel sampleModel = new BuildSampleModel();

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
            Bind<int, int>(() => viewModel.Value, BuildOnValueChanged);
        }

        private void BuildOnValueChanged(int value)
        {
            LastValue = value;
            Debug.Log($"SampleView value changed: {value}");
        }
    }
}






