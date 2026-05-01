using System;
using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class TypePlacementAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenSecondTopLevelTypeIsDeclaredBeforePrimaryNameMatch()
    {
        const string source = @"
namespace Scaffold.GameEngine
{
    internal enum GameState
    {
        Initializing,
        Started,
        Finished
    }

    public sealed class Game
    {
        private GameState state;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\GameEngine\Runtime\Game.cs",
            new TypePlacementAnalyzer(),
            TypePlacementAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
        Assert.Contains("GameState", diagnostics[0].GetMessage(null), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diagnostic_WhenSecondTopLevelTypeIsDeclaredAfterPrimaryNameMatch()
    {
        const string source = @"
namespace Scaffold.GameEngine
{
    public sealed class Game
    {
        private GameState state;
    }

    internal enum GameState
    {
        Initializing,
        Started,
        Finished
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\GameEngine\Runtime\Game.cs",
            new TypePlacementAnalyzer(),
            TypePlacementAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenExtraTypeIsExposedInPublicApi()
    {
        const string source = @"
namespace Scaffold.GameEngine
{
    public sealed class Game
    {
        public GameState State { get; private set; }
    }

    internal enum GameState
    {
        Initializing,
        Started,
        Finished
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\GameEngine\Runtime\Game.cs",
            new TypePlacementAnalyzer(),
            TypePlacementAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_TwoExtras_WhenThreeTopLevelTypes()
    {
        const string source = @"
namespace Scaffold.GameEngine
{
    public sealed class Game
    {
        private GameState state;
    }

    public sealed class GameRunner
    {
        private GameState state;
    }

    internal enum GameState
    {
        Initializing,
        Started,
        Finished
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\GameEngine\Runtime\Game.cs",
            new TypePlacementAnalyzer(),
            TypePlacementAnalyzer.DiagnosticId);

        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public async Task NoDiagnostic_WhenFileContainsSingleTopLevelType()
    {
        const string source = @"
namespace Scaffold.GameEngine
{
    public sealed class Game
    {
        public void Start()
        {
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\GameEngine\Runtime\Game.cs",
            new TypePlacementAnalyzer(),
            TypePlacementAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNestedTypeOnly()
    {
        const string source = @"
namespace Scaffold.GameEngine
{
    public sealed class Game
    {
        private enum LocalState { A, B }

        private LocalState state;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\GameEngine\Runtime\Game.cs",
            new TypePlacementAnalyzer(),
            TypePlacementAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenSameSimpleNameDistinctTypeParameterArities()
    {
        const string source = @"
namespace App.CloudCode
{
    public interface IRequestHandler
    {
    }

    public interface IRequestHandler<TResponse>
    {
    }

    public interface IRequestHandler<TRequest, TResponse> where TRequest : class
    {
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Packages\com.scaffold.cloudcode\Runtime\IRequestHandler.cs",
            new TypePlacementAnalyzer(),
            TypePlacementAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }
}
