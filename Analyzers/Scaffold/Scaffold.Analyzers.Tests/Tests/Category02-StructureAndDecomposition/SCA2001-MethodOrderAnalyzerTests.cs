using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class MethodOrderAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenCalleeIsDeclaredBeforeCaller()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        private void Setup() { }

        public void Initialize()
        {
            Setup();
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new MethodOrderAnalyzer(),
            MethodOrderAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenCallerAppearsBeforeCallee()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Initialize()
        {
            Setup();
        }

        private void Setup() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new MethodOrderAnalyzer(),
            MethodOrderAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenUnrelatedMethodIsBetweenCallerAndCallee()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void A()
        {
            C();
        }

        private void B()
        {
            Do();
        }

        private void C()
        {
            Do();
        }

        private void Do() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new MethodOrderAnalyzer(),
            MethodOrderAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenInterveningMethodAlsoDependsOnDependency()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void A()
        {
            C();
        }

        private void B()
        {
            C();
        }

        private void C() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new MethodOrderAnalyzer(),
            MethodOrderAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_ForStaticMethods()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        private static void C() { }
        private static void A()
        {
            C();
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new MethodOrderAnalyzer(),
            MethodOrderAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }
}
