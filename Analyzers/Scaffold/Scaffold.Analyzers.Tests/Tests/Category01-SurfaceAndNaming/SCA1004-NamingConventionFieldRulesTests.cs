using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class NamingConventionFieldRulesTests
{
    [Fact]
    public async Task Diagnostic_WhenPrivateFieldUsesUnderscorePrefix()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        private int _count;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new NamingConventionAnalyzer(),
            NamingConventionAnalyzer.DiagnosticIdPrefix);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenPrivateFieldUsesSingleLetterHungarianPrefix()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        private int s_count;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new NamingConventionAnalyzer(),
            NamingConventionAnalyzer.DiagnosticIdPrefix);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenPrivateFieldStartsWithUppercase()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        private int Count;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new NamingConventionAnalyzer(),
            NamingConventionAnalyzer.DiagnosticIdPrefix);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenInternalFieldStartsWithLowercase()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        internal int count;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new NamingConventionAnalyzer(),
            NamingConventionAnalyzer.DiagnosticIdPascal);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_ForUnityExemptFieldNames()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public int gameObject;
        public int transform;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new NamingConventionAnalyzer(),
            NamingConventionAnalyzer.DiagnosticIdPascal);

        Assert.Empty(diagnostics);
    }
}
