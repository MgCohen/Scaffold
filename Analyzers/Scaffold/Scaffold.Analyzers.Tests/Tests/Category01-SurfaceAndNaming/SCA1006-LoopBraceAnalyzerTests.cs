using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class LoopBraceAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenLoopBodyHasNoBraces()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            for (var i = 0; i < 3; i++)
                Tick();
        }

        private void Tick() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LoopBraceAnalyzer(),
            LoopBraceAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenLoopOpeningBraceIsOnSameLine()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            while (true) { Tick(); break; }
        }

        private void Tick() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LoopBraceAnalyzer(),
            LoopBraceAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenLoopUsesBracesOnNextLine()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            foreach (var value in values)
            {
                Tick(value);
            }
        }

        private int[] values = new int[0];
        private void Tick(int value) { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LoopBraceAnalyzer(),
            LoopBraceAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }
}
