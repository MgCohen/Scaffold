using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class ConditionalBraceAnalyzerTests
{
    [Fact]
    public async Task NoDiagnostic_ForInlineSingleStatementIfWithoutElse()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute(bool enabled)
        {
            if (enabled) Tick();
        }

        private void Tick() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new ConditionalBraceAnalyzer(),
            ConditionalBraceAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenIfWithoutElseIsNotInlineSingleStatement()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute(bool enabled)
        {
            if (enabled)
                Tick();
        }

        private void Tick() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new ConditionalBraceAnalyzer(),
            ConditionalBraceAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenElseBranchHasNoBraces()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute(bool enabled)
        {
            if (enabled)
            {
                Tick();
            }
            else Tick();
        }

        private void Tick() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new ConditionalBraceAnalyzer(),
            ConditionalBraceAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }
}
