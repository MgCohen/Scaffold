using System.Threading.Tasks;
using Scaffold.Mvvm.Analyzers;
using Xunit;

namespace Scaffold.Mvvm.Analyzers.Tests;

public sealed class MvvmAttributeReferenceAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenObservablePropertyAttributeIsUnresolved()
    {
        const string source = @"
namespace Scaffold.MVVM
{
    public partial class InventoryViewModel
    {
        [ObservableProperty]
        private int amount;
    }
}";

        var diagnostics = await MvvmAnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\MVVM\Runtime\Implementation\InventoryViewModel.cs",
            new MvvmAttributeReferenceAnalyzer(),
            MvvmAttributeReferenceAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
        Assert.Equal(MvvmAttributeReferenceAnalyzer.DiagnosticId, diagnostics[0].Id);
    }

    [Fact]
    public async Task NoDiagnostic_WhenObservablePropertyAttributeResolves()
    {
        const string source = @"
using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CommunityToolkit.Mvvm.ComponentModel
{
    public sealed class ObservablePropertyAttribute : Attribute {}
}

namespace Scaffold.MVVM
{
    public partial class InventoryViewModel
    {
        [ObservableProperty]
        private int amount;
    }
}";

        var diagnostics = await MvvmAnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\MVVM\Runtime\Implementation\InventoryViewModel.cs",
            new MvvmAttributeReferenceAnalyzer(),
            MvvmAttributeReferenceAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenBindSourceAttributeIsUnresolved()
    {
        const string source = @"
namespace Scaffold.MVVM
{
    [BindSource(typeof(object))]
    public partial class InventoryViewModel
    {
    }
}";

        var diagnostics = await MvvmAnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\MVVM\Runtime\Implementation\InventoryViewModel.cs",
            new MvvmAttributeReferenceAnalyzer(),
            MvvmAttributeReferenceAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
        Assert.Equal(MvvmAttributeReferenceAnalyzer.DiagnosticId, diagnostics[0].Id);
    }
}
