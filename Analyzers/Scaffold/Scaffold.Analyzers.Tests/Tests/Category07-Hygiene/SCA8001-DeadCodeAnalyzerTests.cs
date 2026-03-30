using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class DeadCodeInRuntimeAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_ForUnusedPrivateRuntimeMethod()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        private void NotUsed() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Feature\Runtime\Sample.cs",
            new DeadCodeInRuntimeAnalyzer(),
            DeadCodeInRuntimeAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_ForUsedPrivateRuntimeMethod()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Run()
        {
            NotUsed();
        }

        private void NotUsed() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Feature\Runtime\Sample.cs",
            new DeadCodeInRuntimeAnalyzer(),
            DeadCodeInRuntimeAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_ForPublicApiMember()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void NotUsed() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Feature\Runtime\Sample.cs",
            new DeadCodeInRuntimeAnalyzer(),
            DeadCodeInRuntimeAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_ForInterfaceImplementation()
    {
        const string source = @"
namespace Demo
{
    public interface IRun
    {
        void Execute();
    }

    public class Sample : IRun
    {
        void IRun.Execute() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Feature\Runtime\Sample.cs",
            new DeadCodeInRuntimeAnalyzer(),
            DeadCodeInRuntimeAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_ForTestsPath()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        private void NotUsed() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Feature\Tests\Sample.cs",
            new DeadCodeInRuntimeAnalyzer(),
            DeadCodeInRuntimeAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_ForUnityMessageLikeMethodName()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        private void OnEnable() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Feature\Runtime\Sample.cs",
            new DeadCodeInRuntimeAnalyzer(),
            DeadCodeInRuntimeAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }
}
