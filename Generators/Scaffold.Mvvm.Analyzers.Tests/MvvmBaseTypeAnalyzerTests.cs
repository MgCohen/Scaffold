using System.Threading.Tasks;
using Scaffold.Mvvm.Analyzers;
using Xunit;

namespace Scaffold.Mvvm.Analyzers.Tests;

public sealed class MvvmBaseTypeAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenClassImplementsIViewModelWithoutViewModelBase()
    {
        const string source = @"
namespace Scaffold.MVVM
{
    public interface IViewModel {}

    public class InventoryViewModel : IViewModel
    {
    }
}";

        var diagnostics = await MvvmAnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\MVVM\Runtime\Implementation\InventoryViewModel.cs",
            new MvvmBaseTypeAnalyzer(),
            MvvmBaseTypeAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
        Assert.Equal(MvvmBaseTypeAnalyzer.DiagnosticId, diagnostics[0].Id);
    }

    [Fact]
    public async Task NoDiagnostic_WhenClassInheritsViewModel()
    {
        const string source = @"
namespace Scaffold.MVVM
{
    public interface IViewModel {}

    public abstract class ViewModel : IViewModel
    {
    }

    public partial class InventoryViewModel : ViewModel
    {
    }
}";

        var diagnostics = await MvvmAnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\MVVM\Runtime\Implementation\InventoryViewModel.cs",
            new MvvmBaseTypeAnalyzer(),
            MvvmBaseTypeAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenClassImplementsInpcWithoutModelOrViewModelBase()
    {
        const string source = @"
namespace System.ComponentModel
{
    public interface INotifyPropertyChanged {}
}

namespace Scaffold.MVVM
{
    public class InventoryState : System.ComponentModel.INotifyPropertyChanged
    {
    }
}";

        var diagnostics = await MvvmAnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\MVVM\Runtime\Implementation\InventoryState.cs",
            new MvvmBaseTypeAnalyzer(),
            MvvmBaseTypeAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
        Assert.Equal(MvvmBaseTypeAnalyzer.DiagnosticId, diagnostics[0].Id);
    }

    [Fact]
    public async Task NoDiagnostic_WhenClassIsOutsideMvvmModulePath()
    {
        const string source = @"
namespace System.ComponentModel
{
    public interface INotifyPropertyChanged {}
}

namespace Scaffold.Navigation
{
    public class InventoryState : System.ComponentModel.INotifyPropertyChanged
    {
    }
}";

        var diagnostics = await MvvmAnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Runtime\InventoryState.cs",
            new MvvmBaseTypeAnalyzer(),
            MvvmBaseTypeAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenClassImplementsIViewModelViaCustomBaseType()
    {
        const string source = @"
namespace Scaffold.MVVM
{
    public interface IViewModel {}

    public abstract class CustomControllerBase
    {
    }

    public class InventoryViewModel : CustomControllerBase, IViewModel
    {
    }
}";

        var diagnostics = await MvvmAnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\MVVM\Runtime\Implementation\InventoryViewModel.cs",
            new MvvmBaseTypeAnalyzer(),
            MvvmBaseTypeAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }
}
