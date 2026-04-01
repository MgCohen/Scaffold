using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class MethodCommentAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenMethodHasRegularLeadingComment()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        // bad comment
        public void Execute() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new MethodCommentAnalyzer(),
            MethodCommentAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenCommentContainsTodo()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        // todo: wire this up
        public void Execute() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new MethodCommentAnalyzer(),
            MethodCommentAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }
}
