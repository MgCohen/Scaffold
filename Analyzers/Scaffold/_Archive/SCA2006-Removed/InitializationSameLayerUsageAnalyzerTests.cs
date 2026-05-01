using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class InitializationSameLayerUsageAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenInitializeAsyncForwardsSameLayerDependencyAcrossStructuralFiles()
    {
        var graph = StructuralTestGraph
            .Create("Scaffold.Level.Runtime")
            .Assembly("Scaffold.Level.Runtime")
                .WithSource(
                    "Assets/Scripts/Meta/Level/Runtime/LevelService.cs",
                    @"
namespace Scaffold.Scope.Contracts
{
    public interface IAsyncLayerInitializable
    {
        System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken);
    }
}

namespace Scaffold.Meta.Level
{
    public sealed class LevelService : Scaffold.Scope.Contracts.IAsyncLayerInitializable
    {
        private readonly GoldService goldService;
        private readonly LevelBootstrapChain chain = new LevelBootstrapChain();

        public LevelService(GoldService goldService)
        {
            this.goldService = goldService;
        }

        public System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken)
        {
            chain.Probe(goldService);
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}")
                .WithSource(
                    "Assets/Scripts/Meta/Level/Runtime/LevelBootstrapChain.cs",
                    @"
namespace Scaffold.Meta.Level
{
    public sealed class LevelBootstrapChain
    {
        public void Probe(GoldService dependency)
        {
            int gold = dependency.GetCurrentGold();
        }
    }

    public sealed class GoldService
    {
        public int GetCurrentGold() => 10;
    }
}")
            .Build();

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            graph,
            new InitializationSameLayerUsageAnalyzer(),
            InitializationSameLayerUsageAnalyzer.DiagnosticId);

        Assert.True(diagnostics.Length >= 1);
    }

    [Fact]
    public async Task NoDiagnostic_WhenForwardedCallChainMethodHasAllowInitializationCallChainAttribute()
    {
        var graph = StructuralTestGraph
            .Create("Scaffold.Level.Runtime")
            .Assembly("Scaffold.Level.Runtime")
                .WithSource(
                    "Assets/Scripts/Meta/Level/Runtime/LevelService.cs",
                    @"
namespace Scaffold.Scope.Contracts
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
    public sealed class AllowInitializationCallChainAttribute : System.Attribute { }

    public interface IAsyncLayerInitializable
    {
        System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken);
    }
}

namespace Scaffold.Meta.Level
{
    public sealed class LevelService : Scaffold.Scope.Contracts.IAsyncLayerInitializable
    {
        private readonly GoldService goldService;

        public LevelService(GoldService goldService)
        {
            this.goldService = goldService;
        }

        public System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken)
        {
            LevelBootstrapChain.Probe(goldService);
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}")
                .WithSource(
                    "Assets/Scripts/Meta/Level/Runtime/LevelBootstrapChain.cs",
                    @"
namespace Scaffold.Meta.Level
{
    public static class LevelBootstrapChain
    {
        [Scaffold.Scope.Contracts.AllowInitializationCallChain]
        public static void Probe(GoldService dependency)
        {
            int gold = dependency.GetCurrentGold();
        }
    }

    public sealed class GoldService
    {
        public int GetCurrentGold() => 10;
    }
}")
            .Build();

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            graph,
            new InitializationSameLayerUsageAnalyzer(),
            InitializationSameLayerUsageAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenInitializeAsyncOnlyStoresOrPassesDependencyReference()
    {
        const string source = @"
namespace Scaffold.Scope.Contracts
{
    public interface IAsyncLayerInitializable
    {
        System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken);
    }
}

namespace Scaffold.Meta.Level
{
    public sealed class GoldService
    {
        public int GetCurrentGold() => 10;
    }

    public sealed class LevelService : Scaffold.Scope.Contracts.IAsyncLayerInitializable
    {
        private GoldService goldService;

        public LevelService(GoldService goldService)
        {
            this.goldService = goldService;
        }

        public System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken)
        {
            Persist(goldService);
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private void Persist(GoldService dependency)
        {
            this.goldService = dependency;
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Meta\Level\Runtime\LevelService.cs",
            new InitializationSameLayerUsageAnalyzer(),
            InitializationSameLayerUsageAnalyzer.DiagnosticId,
            compilationAssemblyName: "Scaffold.Level.Runtime");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenInitializeAsyncCallsSameLayerDependencyMethod()
    {
        const string source = @"
namespace Scaffold.Scope.Contracts
{
    public interface IAsyncLayerInitializable
    {
        System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken);
    }
}

namespace Scaffold.Meta.Level
{
    public sealed class GoldService
    {
        public int GetCurrentGold() => 10;
    }

    public sealed class LevelService : Scaffold.Scope.Contracts.IAsyncLayerInitializable
    {
        private readonly GoldService goldService;

        public LevelService(GoldService goldService)
        {
            this.goldService = goldService;
        }

        public System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken)
        {
            int gold = goldService.GetCurrentGold();
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Meta\Level\Runtime\LevelService.cs",
            new InitializationSameLayerUsageAnalyzer(),
            InitializationSameLayerUsageAnalyzer.DiagnosticId,
            compilationAssemblyName: "Scaffold.Level.Runtime");

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenHelperMethodUsesForwardedSameLayerDependency()
    {
        const string source = @"
namespace Scaffold.Scope.Contracts
{
    public interface IAsyncLayerInitializable
    {
        System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken);
    }
}

namespace Scaffold.Meta.Level
{
    public sealed class GoldService
    {
        public int GetCurrentGold() => 10;
    }

    public sealed class LevelService : Scaffold.Scope.Contracts.IAsyncLayerInitializable
    {
        private readonly GoldService goldService;

        public LevelService(GoldService goldService)
        {
            this.goldService = goldService;
        }

        public System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken)
        {
            Probe(goldService);
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private void Probe(GoldService dependency)
        {
            int gold = dependency.GetCurrentGold();
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Meta\Level\Runtime\LevelService.cs",
            new InitializationSameLayerUsageAnalyzer(),
            InitializationSameLayerUsageAnalyzer.DiagnosticId,
            compilationAssemblyName: "Scaffold.Level.Runtime");

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenCallChainExceptionAttributeIsApplied()
    {
        const string source = @"
namespace Scaffold.Scope.Contracts
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
    public sealed class AllowInitializationCallChainAttribute : System.Attribute { }

    public interface IAsyncLayerInitializable
    {
        System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken);
    }
}

namespace Scaffold.Meta.Level
{
    public sealed class GoldService
    {
        public int GetCurrentGold() => 10;
    }

    public sealed class LevelService : Scaffold.Scope.Contracts.IAsyncLayerInitializable
    {
        private readonly GoldService goldService;

        public LevelService(GoldService goldService)
        {
            this.goldService = goldService;
        }

        public System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken)
        {
            Probe(goldService);
            return System.Threading.Tasks.Task.CompletedTask;
        }

        [Scaffold.Scope.Contracts.AllowInitializationCallChain]
        private void Probe(GoldService dependency)
        {
            int gold = dependency.GetCurrentGold();
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Meta\Level\Runtime\LevelService.cs",
            new InitializationSameLayerUsageAnalyzer(),
            InitializationSameLayerUsageAnalyzer.DiagnosticId,
            compilationAssemblyName: "Scaffold.Level.Runtime");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenUsageExceptionAttributeIsApplied()
    {
        const string source = @"
namespace Scaffold.Scope.Contracts
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, Inherited = false)]
    public sealed class AllowSameLayerInitializationUsageAttribute : System.Attribute { }

    public interface IAsyncLayerInitializable
    {
        System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken);
    }
}

namespace Scaffold.Meta.Level
{
    public sealed class GoldService
    {
        public int GetCurrentGold() => 10;
    }

    public sealed class LevelService : Scaffold.Scope.Contracts.IAsyncLayerInitializable
    {
        private readonly GoldService goldService;

        public LevelService(GoldService goldService)
        {
            this.goldService = goldService;
        }

        [Scaffold.Scope.Contracts.AllowSameLayerInitializationUsage]
        public System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken)
        {
            int gold = goldService.GetCurrentGold();
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Meta\Level\Runtime\LevelService.cs",
            new InitializationSameLayerUsageAnalyzer(),
            InitializationSameLayerUsageAnalyzer.DiagnosticId,
            compilationAssemblyName: "Scaffold.Level.Runtime");

        Assert.Empty(diagnostics);
    }
}

