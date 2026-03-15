using CommunityToolkit.Mvvm.ComponentModel;
using NUnit.Framework;
using Scaffold.MVVM.Binding;
using UnityEngine;

namespace Scaffold.MVVM.Tests
{
    public class MVVMTests
    {
        [Test]
        public void ViewModel_Bind_UpdatesValueFromNestedModel()
        {
            SampleViewModel viewModel = new SampleViewModel();
            viewModel.Bind(null);
            viewModel.RegisterNestedForTest();
            viewModel.SampleModel.Value = 42;
            Assert.AreEqual(42, viewModel.Value);
        }

        [Test]
        public void View_Bind_SetsInitialValue_FromViewModel()
        {
            SampleViewModel viewModel = new SampleViewModel();
            viewModel.SampleModel.Value = 3;
            using ViewFixture fixture = CreateViewFixture(viewModel);
            Assert.AreEqual(3, fixture.View.LastValue);
        }

        [Test]
        public void View_Bind_UpdatesTarget_WhenViewModelChanges()
        {
            using ViewFixture fixture = CreateViewFixture();
            fixture.ViewModel.SampleModel.Value = 7;
            Assert.AreEqual(7, fixture.View.LastValue);
        }

        private static ViewFixture CreateViewFixture()
        {
            SampleViewModel viewModel = new SampleViewModel();
            return CreateViewFixture(viewModel);
        }

        private static ViewFixture CreateViewFixture(SampleViewModel viewModel)
        {
            viewModel.Bind(null);
            GameObject gameObject = new GameObject(nameof(SampleView));
            SampleView view = gameObject.AddComponent<SampleView>();
            view.Bind(viewModel);
            return new ViewFixture(gameObject, view, viewModel);
        }

        private sealed class ViewFixture : System.IDisposable
        {
            public ViewFixture(GameObject gameObject, SampleView view, SampleViewModel viewModel)
            {
                GameObject = gameObject;
                View = view;
                ViewModel = viewModel;
            }

            public GameObject GameObject { get; }
            public SampleView View { get; }
            public SampleViewModel ViewModel { get; }

            public void Dispose()
            {
                Object.DestroyImmediate(GameObject);
            }
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

        public void RegisterNestedForTest()
        {
            ((INestedObservableProperties)this).RegisterNestedProperties();
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
        }
    }
}
