using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class AnalyzerScopeGateTests
{
    [Fact]
    public async Task ExpressionBody_NoDiagnostic_WhenIgnoredAssemblyNameContainsMatches()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public int Count() => 1;
    }
}";

        var options = new Dictionary<string, string>
        {
            ["scaffold.global.ignored_assembly_name_contains"] = "Tests",
        };

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new ExpressionBodiedMethodAnalyzer(),
            ExpressionBodiedMethodAnalyzer.DiagnosticId,
            analyzerOptions: options,
            compilationAssemblyName: "Consumer.Tests");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ExpressionBody_NoDiagnostic_WhenIgnoredPathMatches()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public int Count() => 1;
    }
}";

        var options = new Dictionary<string, string>
        {
            ["scaffold.global.ignored_paths"] = "Assets/Samples",
        };

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Samples\Splines\Runtime\Sample.cs",
            new ExpressionBodiedMethodAnalyzer(),
            ExpressionBodiedMethodAnalyzer.DiagnosticId,
            analyzerOptions: options,
            compilationAssemblyName: "Unity.Splines.Examples");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ExpressionBody_Diagnostic_WhenNoIgnoreMatch()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public int Count() => 1;
    }
}";

        var options = new Dictionary<string, string>
        {
            ["scaffold.global.ignored_assembly_name_contains"] = "Tests",
            ["scaffold.global.ignored_paths"] = "Assets/Samples",
        };

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new ExpressionBodiedMethodAnalyzer(),
            ExpressionBodiedMethodAnalyzer.DiagnosticId,
            analyzerOptions: options,
            compilationAssemblyName: "Game.Core.Runtime");

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task ExpressionBody_NoDiagnostic_WhenIgnoreInfrastructureAssembliesTrue_AndEditorAssembly()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public int Count() => 1;
    }
}";

        var options = new Dictionary<string, string>
        {
            ["scaffold.global.ignore_infrastructure_assemblies"] = "true",
        };

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Editor\Sample.cs",
            new ExpressionBodiedMethodAnalyzer(),
            ExpressionBodiedMethodAnalyzer.DiagnosticId,
            analyzerOptions: options,
            compilationAssemblyName: "Game.Core.Editor");

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("Tests,Editor")]
    [InlineData("Tests;Editor")]
    [InlineData("Tests\nEditor")]
    public void ConfigurationLists_AcceptSupportedSeparators(string value)
    {
        Assert.Equal(new[] { "Tests", "Editor" }, AnalyzerConfig.ParseSemicolonList(value));
    }
}
