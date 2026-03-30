using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class PragmaWarningDisableAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_ForPragmaDisableInRuntimeFile()
    {
        const string source = @"
#pragma warning disable CS0168
namespace Demo
{
    public class Sample
    {
        public void Run() { }
    }
}
#pragma warning restore CS0168
";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Feature\Runtime\Sample.cs",
            new PragmaWarningDisableAnalyzer(),
            PragmaWarningDisableAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenDisableImmediatelyFollowedByRestoreForSameCodes()
    {
        const string source = @"
#pragma warning disable CS0168
#pragma warning restore CS0168
namespace Demo
{
    public class Sample
    {
        public void Run() { }
    }
}
";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Feature\Runtime\Sample.cs",
            new PragmaWarningDisableAnalyzer(),
            PragmaWarningDisableAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_FromTestDataFile_MatchesInlinePragmaRuntime()
    {
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            AnalyzerTestHarness.LoadTestDataText(@"Category07/SCA8002-PragmaRuntime.cs"),
            @"C:\Repo\Assets\Scripts\Core\Feature\Runtime\Sample.cs",
            new PragmaWarningDisableAnalyzer(),
            PragmaWarningDisableAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_FromTestDataAsync_Helper()
    {
        var all = await AnalyzerTestHarness.GetDiagnosticsFromTestDataAsync(
            @"Category07/SCA8002-PragmaRuntime.cs",
            @"C:\Repo\Assets\Scripts\Core\Feature\Runtime\Sample.cs",
            new PragmaWarningDisableAnalyzer());

        Assert.Single(all.Where(d => d.Id == PragmaWarningDisableAnalyzer.DiagnosticId));
    }

    [Fact]
    public async Task NoDiagnostic_ForPragmaDisableInTestsFile()
    {
        const string source = @"
#pragma warning disable CS0168
namespace Demo
{
    public class Sample
    {
        public void Run() { }
    }
}
#pragma warning restore CS0168
";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Feature\Tests\Sample.cs",
            new PragmaWarningDisableAnalyzer(),
            PragmaWarningDisableAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_ForPragmaDisableInSamplesFile()
    {
        const string source = @"
#pragma warning disable CS0168
namespace Demo
{
    public class Sample
    {
        public void Run() { }
    }
}
#pragma warning restore CS0168
";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Feature\Samples\Sample.cs",
            new PragmaWarningDisableAnalyzer(),
            PragmaWarningDisableAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenRuleSeverityIsNone()
    {
        const string source = @"
#pragma warning disable CS0168
namespace Demo
{
    public class Sample
    {
        public void Run() { }
    }
}
#pragma warning restore CS0168
";

        var options = AnalyzerTestHarness.CreateDotnetDiagnosticSeverityOptions(
            PragmaWarningDisableAnalyzer.DiagnosticId,
            "none");

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Feature\Runtime\Sample.cs",
            new PragmaWarningDisableAnalyzer(),
            PragmaWarningDisableAnalyzer.DiagnosticId,
            options);

        Assert.Empty(diagnostics);
    }
}
