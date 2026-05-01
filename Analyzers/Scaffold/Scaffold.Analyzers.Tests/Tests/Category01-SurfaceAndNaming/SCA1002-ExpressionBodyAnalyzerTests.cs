using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class ExpressionBodiedMethodAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenMethodUsesExpressionBody()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public int Count() => 1;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new ExpressionBodiedMethodAnalyzer(),
            ExpressionBodiedMethodAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task DiagnosticMessageContainsMethodName_Regression()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public int Count() => 1;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new ExpressionBodiedMethodAnalyzer(),
            ExpressionBodiedMethodAnalyzer.DiagnosticId);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("Count", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Diagnostic_WhenConstructorUsesExpressionBody()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public Sample() => Initialize();

        private void Initialize() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new ExpressionBodiedMethodAnalyzer(),
            ExpressionBodiedMethodAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenOverrideUsesExpressionBody()
    {
        const string source = @"
namespace Demo
{
    public class Sample : object
    {
        public override int GetHashCode() => 42;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new ExpressionBodiedMethodAnalyzer(),
            ExpressionBodiedMethodAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenExplicitInterfaceImplementationUsesExpressionBody()
    {
        const string source = @"
namespace Demo
{
    public interface IFoo { int Bar(); }

    public class Sample : IFoo
    {
        int IFoo.Bar() => 1;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new ExpressionBodiedMethodAnalyzer(),
            ExpressionBodiedMethodAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }
}
