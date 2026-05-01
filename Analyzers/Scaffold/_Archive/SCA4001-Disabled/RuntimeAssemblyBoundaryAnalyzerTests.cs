using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class RuntimeAssemblyBoundaryAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenNonBootstrapReferencesForeignRuntimeAssembly()
    {
        var graph = StructuralTestGraph
            .Create("Scaffold.MainMenu.Runtime")
            .Assembly("Scaffold.MainMenu.Runtime")
                .WithSource(
                    "Assets/Scripts/App/MainMenu/Runtime/MenuPresenter.cs",
                    @"namespace Scaffold.MainMenu { public sealed class MenuPresenter { } }")
                .References("Scaffold.Meta.Gold.Runtime")
            .Assembly("Scaffold.Meta.Gold.Runtime")
                .WithSource(
                    "Assets/Scripts/Meta/Gold/Runtime/GoldService.cs",
                    @"namespace Scaffold.Meta.Gold { public sealed class GoldService { } }")
            .Assembly("Scaffold.Meta.Gold.Contracts")
                .WithSource(
                    "Assets/Scripts/Meta/Gold/Contracts/IGoldService.cs",
                    @"namespace Scaffold.Meta.Gold.Contracts { public interface IGoldService { } }")
            .Build();

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            graph,
            new RuntimeAssemblyBoundaryAnalyzer(),
            RuntimeAssemblyBoundaryAnalyzer.DiagnosticId);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("Scaffold.MainMenu.Runtime", diagnostic.GetMessage());
        Assert.Contains("Scaffold.Meta.Gold.Runtime", diagnostic.GetMessage());
    }

    [Fact]
    public async Task NoDiagnostic_WhenCompositionRootReferencesForeignRuntimeAssembly()
    {
        var graph = StructuralTestGraph
            .Create("Scaffold.AppHost.Runtime")
            .Assembly("Scaffold.AppHost.Runtime")
                .WithSource(
                    "Assets/Scripts/App/Runtime/AppCompositionRoot.cs",
                    @"namespace Scaffold.AppHost { public sealed class AppCompositionRoot { } }")
                .References("Scaffold.Meta.Gold.Runtime")
            .Assembly("Scaffold.Meta.Gold.Runtime")
                .WithSource(
                    "Assets/Scripts/Meta/Gold/Runtime/GoldService.cs",
                    @"namespace Scaffold.Meta.Gold { public sealed class GoldService { } }")
            .Build();

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            graph,
            new RuntimeAssemblyBoundaryAnalyzer(),
            RuntimeAssemblyBoundaryAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenReferencingOwnRuntimeAssembly()
    {
        var graph = StructuralTestGraph
            .Create("Scaffold.Meta.Gold.Tests")
            .Assembly("Scaffold.Meta.Gold.Tests")
                .WithSource(
                    "Assets/Scripts/Meta/Gold/Tests/GoldTests.cs",
                    @"namespace Scaffold.Meta.Gold.Tests { public sealed class GoldTests { } }")
                .References("Scaffold.Meta.Gold.Runtime")
            .Assembly("Scaffold.Meta.Gold.Runtime")
                .WithSource(
                    "Assets/Scripts/Meta/Gold/Runtime/GoldService.cs",
                    @"namespace Scaffold.Meta.Gold { public sealed class GoldService { } }")
            .Build();

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            graph,
            new RuntimeAssemblyBoundaryAnalyzer(),
            RuntimeAssemblyBoundaryAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenOnlyContractsAreReferenced()
    {
        var graph = StructuralTestGraph
            .Create("Scaffold.MainMenu.Runtime")
            .Assembly("Scaffold.MainMenu.Runtime")
                .WithSource(
                    "Assets/Scripts/App/MainMenu/Runtime/MenuPresenter.cs",
                    @"namespace Scaffold.MainMenu { public sealed class MenuPresenter { } }")
                .References("Scaffold.Meta.Gold.Contracts")
            .Assembly("Scaffold.Meta.Gold.Contracts")
                .WithSource(
                    "Assets/Scripts/Meta/Gold/Contracts/IGoldService.cs",
                    @"namespace Scaffold.Meta.Gold.Contracts { public interface IGoldService { } }")
            .Build();

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            graph,
            new RuntimeAssemblyBoundaryAnalyzer(),
            RuntimeAssemblyBoundaryAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenTestAssemblyReferencesForeignRuntimeAssembly()
    {
        var graph = StructuralTestGraph
            .Create("Scaffold.MainMenu.Tests")
            .Assembly("Scaffold.MainMenu.Tests")
                .WithSource(
                    "Assets/Scripts/App/MainMenu/Tests/MenuPresenterTests.cs",
                    @"namespace Scaffold.MainMenu.Tests { public sealed class MenuPresenterTests { } }")
                .References("Scaffold.Meta.Gold.Runtime")
            .Assembly("Scaffold.Meta.Gold.Runtime")
                .WithSource(
                    "Assets/Scripts/Meta/Gold/Runtime/GoldService.cs",
                    @"namespace Scaffold.Meta.Gold { public sealed class GoldService { } }")
            .Build();

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            graph,
            new RuntimeAssemblyBoundaryAnalyzer(),
            RuntimeAssemblyBoundaryAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenContainerAssemblyReferencesForeignRuntimeAssembly()
    {
        var graph = StructuralTestGraph
            .Create("Scaffold.Navigation.Container")
            .Assembly("Scaffold.Navigation.Container")
                .WithSource(
                    "Assets/Scripts/Infra/Navigation/Container/NavigationInstaller.cs",
                    @"namespace Scaffold.Navigation.Container { public sealed class NavigationInstaller { } }")
                .References("Scaffold.Navigation.Runtime")
            .Assembly("Scaffold.Navigation.Runtime")
                .WithSource(
                    "Assets/Scripts/Infra/Navigation/Runtime/NavigationService.cs",
                    @"namespace Scaffold.Navigation { public sealed class NavigationService { } }")
            .Build();

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            graph,
            new RuntimeAssemblyBoundaryAnalyzer(),
            RuntimeAssemblyBoundaryAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenReferencedRuntimeModuleHasNoContractsFolder()
    {
        var graph = StructuralTestGraph
            .Create("Scaffold.MainMenu.Runtime")
            .Assembly("Scaffold.MainMenu.Runtime")
                .WithSource(
                    "Assets/Scripts/App/MainMenu/Runtime/MenuPresenter.cs",
                    @"namespace Scaffold.MainMenu { public sealed class MenuPresenter { } }")
                .References("Scaffold.Meta.Gold.Runtime")
            .Assembly("Scaffold.Meta.Gold.Runtime")
                .WithSource(
                    "Assets/Scripts/Meta/Gold/Runtime/GoldService.cs",
                    @"namespace Scaffold.Meta.Gold { public sealed class GoldService { } }")
            .Build();

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            graph,
            new RuntimeAssemblyBoundaryAnalyzer(),
            RuntimeAssemblyBoundaryAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenReferencedRuntimeModuleHasContractsFolder()
    {
        var graph = StructuralTestGraph
            .Create("Scaffold.MainMenu.Runtime")
            .Assembly("Scaffold.MainMenu.Runtime")
                .WithSource(
                    "Assets/Scripts/App/MainMenu/Runtime/MenuPresenter.cs",
                    @"namespace Scaffold.MainMenu { public sealed class MenuPresenter { } }")
                .References("Scaffold.Meta.Gold.Runtime")
            .Assembly("Scaffold.Meta.Gold.Runtime")
                .WithSource(
                    "Assets/Scripts/Meta/Gold/Runtime/GoldService.cs",
                    @"namespace Scaffold.Meta.Gold { public sealed class GoldService { } }")
            .Assembly("Scaffold.Meta.Gold.Contracts")
                .WithSource(
                    "Assets/Scripts/Meta/Gold/Contracts/IGoldService.cs",
                    @"namespace Scaffold.Meta.Gold.Contracts { public interface IGoldService { } }")
            .Build();

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            graph,
            new RuntimeAssemblyBoundaryAnalyzer(),
            RuntimeAssemblyBoundaryAnalyzer.DiagnosticId);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("Scaffold.MainMenu.Runtime", diagnostic.GetMessage());
        Assert.Contains("Scaffold.Meta.Gold.Runtime", diagnostic.GetMessage());
    }

    [Fact]
    public async Task NoDiagnostic_WhenReferencedModuleIsConfiguredAsNoContractModule()
    {
        var graph = StructuralTestGraph
            .Create("Scaffold.MainMenu.Runtime")
            .Assembly("Scaffold.MainMenu.Runtime")
                .WithSource(
                    "Assets/Scripts/App/MainMenu/Runtime/MenuPresenter.cs",
                    @"namespace Scaffold.MainMenu { public sealed class MenuPresenter { } }")
                .References("Scaffold.Meta.Gold.Runtime")
            .Assembly("Scaffold.Meta.Gold.Runtime")
                .WithSource(
                    "Assets/Scripts/Meta/Gold/Runtime/GoldService.cs",
                    @"namespace Scaffold.Meta.Gold { public sealed class GoldService { } }")
            .Assembly("Scaffold.Meta.Gold.Contracts")
                .WithSource(
                    "Assets/Scripts/Meta/Gold/Contracts/IGoldService.cs",
                    @"namespace Scaffold.Meta.Gold.Contracts { public interface IGoldService { } }")
            .Build();

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            graph,
            new RuntimeAssemblyBoundaryAnalyzer(),
            RuntimeAssemblyBoundaryAnalyzer.DiagnosticId,
            analyzerOptions: new System.Collections.Generic.Dictionary<string, string>
            {
                ["scaffold.SCA4001.no_contract_modules"] = "Scaffold.Meta.Gold"
            });

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenExternalRuntimeAssemblyIsReferenced()
    {
        const string source = @"namespace Scaffold.MainMenu { public sealed class MenuPresenter { } }";
        var graph = StructuralTestGraph
            .Create("Scaffold.MainMenu.Runtime")
            .Assembly("Scaffold.MainMenu.Runtime")
                .WithSource("Assets/Scripts/App/MainMenu/Runtime/MenuPresenter.cs", source)
                .References("System.Runtime")
            .Build();

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            graph,
            new RuntimeAssemblyBoundaryAnalyzer(),
            RuntimeAssemblyBoundaryAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }
}
