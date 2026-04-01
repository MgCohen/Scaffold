using System.Threading.Tasks;
using Scaffold.Mvvm.Analyzers;
using Xunit;

namespace Scaffold.Mvvm.Analyzers.Tests;

public sealed class MvvmBindApiAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenViewModelDescendantUsesPropertyChangedSubscription()
    {
        const string source = @"
namespace System.ComponentModel
{
    public class PropertyChangedEventArgs : System.EventArgs {}
    public delegate void PropertyChangedEventHandler(object sender, PropertyChangedEventArgs e);
    public interface INotifyPropertyChanged
    {
        event PropertyChangedEventHandler PropertyChanged;
    }
}

namespace Scaffold.MVVM
{
    public abstract class ViewModel {}

    public sealed class InventoryViewModel : ViewModel
    {
        public void BindTo(System.ComponentModel.INotifyPropertyChanged model)
        {
            model.PropertyChanged += OnChanged;
        }

        private void OnChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
        }
    }
}";

        var diagnostics = await MvvmAnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\App\MainMenu\Runtime\InventoryViewModel.cs",
            new MvvmBindApiAnalyzer(),
            MvvmBindApiAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenViewElementDescendantUsesPropertyChangedSubscription()
    {
        const string source = @"
namespace System.ComponentModel
{
    public class PropertyChangedEventArgs : System.EventArgs {}
    public delegate void PropertyChangedEventHandler(object sender, PropertyChangedEventArgs e);
    public interface INotifyPropertyChanged
    {
        event PropertyChangedEventHandler PropertyChanged;
    }
}

namespace Scaffold.MVVM
{
    public abstract class ViewElement {}

    public sealed class InventoryView : ViewElement
    {
        public void Attach(System.ComponentModel.INotifyPropertyChanged vm)
        {
            vm.PropertyChanged += OnVmChanged;
        }

        private void OnVmChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
        }
    }
}";

        var diagnostics = await MvvmAnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\App\MainMenu\Runtime\InventoryView.cs",
            new MvvmBindApiAnalyzer(),
            MvvmBindApiAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenModelDescendantDeclaresPropertyChangedEvent()
    {
        const string source = @"
namespace System.ComponentModel
{
    public class PropertyChangedEventArgs : System.EventArgs {}
    public delegate void PropertyChangedEventHandler(object sender, PropertyChangedEventArgs e);
}

namespace Scaffold.MVVM
{
    public abstract class Model {}

    public sealed class InventoryModel : Model
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }
}";

        var diagnostics = await MvvmAnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\MVVM\Runtime\Implementation\InventoryModel.cs",
            new MvvmBindApiAnalyzer(),
            MvvmBindApiAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenMvvmDescendantDoesNotUseManualPropertyChanged()
    {
        const string source = @"
namespace Scaffold.MVVM
{
    public abstract class ViewModel {}

    public sealed class InventoryViewModel : ViewModel
    {
        public void Setup()
        {
            BindSomething();
        }

        private void BindSomething()
        {
        }
    }
}";

        var diagnostics = await MvvmAnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\App\MainMenu\Runtime\InventoryViewModel.cs",
            new MvvmBindApiAnalyzer(),
            MvvmBindApiAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenViewModelDescendantCallsUpdateBindingManually()
    {
        const string source = @"
namespace Scaffold.MVVM
{
    public abstract class ViewModel
    {
        protected void UpdateBinding(string key) {}
    }

    public sealed class InventoryViewModel : ViewModel
    {
        public void Refresh()
        {
            UpdateBinding(nameof(Refresh));
        }
    }
}";

        var diagnostics = await MvvmAnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\App\MainMenu\Runtime\InventoryViewModel.cs",
            new MvvmBindApiAnalyzer(),
            MvvmBindApiAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenViewModelDescendantCallsRegisterNestedPropertiesManually()
    {
        const string source = @"
namespace Scaffold.MVVM.Binding
{
    public interface INestedObservableProperties
    {
        void RegisterNestedProperties();
    }
}

namespace Scaffold.MVVM
{
    public abstract class ViewModel : Scaffold.MVVM.Binding.INestedObservableProperties
    {
        public void RegisterNestedProperties() {}
    }

    public sealed class InventoryViewModel : ViewModel
    {
        public void Setup()
        {
            RegisterNestedProperties();
        }
    }
}";

        var diagnostics = await MvvmAnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\App\MainMenu\Runtime\InventoryViewModel.cs",
            new MvvmBindApiAnalyzer(),
            MvvmBindApiAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }
}
