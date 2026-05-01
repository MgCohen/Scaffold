using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class NamingConventionAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenPublicMethodStartsWithLowercase()
    {
        const string source = @"
namespace Demo
{
    public class Processor
    {
        public void processData() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Processor.cs",
            new NamingConventionAnalyzer(),
            NamingConventionAnalyzer.DiagnosticIdPascal);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("processData", diagnostic.GetMessage());
    }

    [Fact]
    public async Task NoDiagnostic_WhenMethodIsPrivate()
    {
        const string source = @"
namespace Demo
{
    public class Processor
    {
        private void processData() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Processor.cs",
            new NamingConventionAnalyzer(),
            NamingConventionAnalyzer.DiagnosticIdPascal);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenInternalMethodStartsWithLowercase()
    {
        const string source = @"
namespace Demo
{
    public class Processor
    {
        internal void processData() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Processor.cs",
            new NamingConventionAnalyzer(),
            NamingConventionAnalyzer.DiagnosticIdPascal);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task OverrideMethodIsSkipped()
    {
        const string source = @"namespace Demo
{
    public class BaseType
    {
        public virtual void processData() { }
    }

    public class DerivedType : BaseType
    {
        public override void processData() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Processor.cs",
            new NamingConventionAnalyzer(),
            NamingConventionAnalyzer.DiagnosticIdPascal);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(4, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
    }

    [Fact]
    public async Task NoDiagnostic_ForOperatorOverload()
    {
        const string source = @"
namespace Demo
{
    public class Number
    {
        public static Number operator +(Number left, Number right) => left;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Number.cs",
            new NamingConventionAnalyzer(),
            NamingConventionAnalyzer.DiagnosticIdPascal);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenSourceIsThirdPartyDotweenPath()
    {
        const string source = @"
namespace DG.Tweening
{
    public class Demo
    {
        public void processData() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Plugins\Demigiant\DOTween\Modules\DOTweenModuleUI.cs",
            new NamingConventionAnalyzer(),
            NamingConventionAnalyzer.DiagnosticIdPascal);

        Assert.Empty(diagnostics);
    }
}
