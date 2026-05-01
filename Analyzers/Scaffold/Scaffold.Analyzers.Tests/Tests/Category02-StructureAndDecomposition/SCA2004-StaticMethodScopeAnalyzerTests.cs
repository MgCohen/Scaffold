using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class StaticMethodScopeAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenStaticMethodIsInNonStaticClassAndNotAllowlisted()
    {
        const string source = @"
namespace Scaffold.GameEngine
{
    public sealed class Game
    {
        private static void EnsureWaitTimeout(int timeout) { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\GameEngine\Runtime\Game.cs",
            new StaticMethodScopeAnalyzer(),
            StaticMethodScopeAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
        Assert.Equal(StaticMethodScopeAnalyzer.DiagnosticId, diagnostics[0].Id);
    }

    [Fact]
    public async Task NoDiagnostic_WhenMethodIsExtensionMethod()
    {
        const string source = @"
namespace Scaffold.Tools
{
    public static class StringExtensions
    {
        public static string ToSnakeCase(this string value)
        {
            return value;
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Tools\Types\Runtime\StringExtensions.cs",
            new StaticMethodScopeAnalyzer(),
            StaticMethodScopeAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenMethodIsInStaticClass()
    {
        const string source = @"
namespace Scaffold.Tools
{
    public static class MathUtility
    {
        public static int Clamp(int value, int min, int max)
        {
            return value;
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Tools\Types\Runtime\MathUtility.cs",
            new StaticMethodScopeAnalyzer(),
            StaticMethodScopeAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenMethodIsInNestedPrivateStaticClassInsideInstanceClass()
    {
        const string source = @"
namespace Scaffold.Features
{
    public sealed class FeatureService
    {
        private static class Pipeline
        {
            internal static void RunStep() { }
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Features\Runtime\FeatureService.cs",
            new StaticMethodScopeAnalyzer(),
            StaticMethodScopeAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenMethodMatchesParsingOrConversionPattern()
    {
        const string source = @"
namespace Scaffold.Meta.Level
{
    public sealed class LevelId
    {
        public static LevelId Parse(string raw)
        {
            return new LevelId();
        }

        public static bool TryParse(string raw, out LevelId id)
        {
            id = new LevelId();
            return true;
        }

        public static LevelId FromString(string raw)
        {
            return new LevelId();
        }

        public static string ToStorageKey(LevelId id)
        {
            return string.Empty;
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Meta\Level\Runtime\LevelId.cs",
            new StaticMethodScopeAnalyzer(),
            StaticMethodScopeAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenMethodMatchesFactoryPattern()
    {
        const string source = @"
namespace Scaffold.Meta.Level
{
    public sealed class LevelFactory
    {
        public static LevelFactory CreateDefault()
        {
            return new LevelFactory();
        }

        public static LevelFactory Build()
        {
            return new LevelFactory();
        }

        public static LevelFactory New()
        {
            return new LevelFactory();
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Meta\Level\Runtime\LevelFactory.cs",
            new StaticMethodScopeAnalyzer(),
            StaticMethodScopeAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }
}
