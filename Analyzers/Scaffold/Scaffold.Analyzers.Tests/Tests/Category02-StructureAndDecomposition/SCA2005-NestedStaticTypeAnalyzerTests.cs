using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class NestedStaticTypeInInstanceAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenNestedStaticInsideInstanceType()
    {
        const string source = @"
namespace Demo
{
    public sealed class Outer
    {
        private static class Pipeline
        {
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new NestedStaticTypeInInstanceAnalyzer(),
            NestedStaticTypeInInstanceAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNestedStaticInsideStaticType()
    {
        const string source = @"
namespace Demo
{
    public static class Outer
    {
        private static class Inner
        {
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new NestedStaticTypeInInstanceAnalyzer(),
            NestedStaticTypeInInstanceAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNestedInstanceTypeInsideInstanceType()
    {
        const string source = @"
namespace Demo
{
    public sealed class Outer
    {
        private sealed class Inner
        {
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new NestedStaticTypeInInstanceAnalyzer(),
            NestedStaticTypeInInstanceAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }
}
