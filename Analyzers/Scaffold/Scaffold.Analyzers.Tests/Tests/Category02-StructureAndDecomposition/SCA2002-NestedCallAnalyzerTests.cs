using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class NestedCallAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenCallIsNestedInsideArgument()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            Process(GetValue());
        }

        private void Process(int value) { }
        private int GetValue() { return 1; }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new NestedCallAnalyzer(),
            NestedCallAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNameofIsUsedAsArgument()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            Log(nameof(Sample));
        }

        private void Log(string value) { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new NestedCallAnalyzer(),
            NestedCallAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenMaxNestingDepthIs2_AndSingleObjectCreationInArgument()
    {
        var options = new Dictionary<string, string>
        {
            [NestedCallAnalyzer.MaxNestingDepthConfigKey] = "2",
        };

        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            Install(new Helper());
        }

        private void Install(Helper h) { }
    }

    public class Helper { }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new NestedCallAnalyzer(),
            NestedCallAnalyzer.DiagnosticId,
            analyzerOptions: options);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenMaxNestingDepthIs2_AndSingleCallInArgument()
    {
        var options = new Dictionary<string, string>
        {
            [NestedCallAnalyzer.MaxNestingDepthConfigKey] = "2",
        };

        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            Process(GetValue());
        }

        private void Process(int value) { }
        private int GetValue() { return 1; }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new NestedCallAnalyzer(),
            NestedCallAnalyzer.DiagnosticId,
            analyzerOptions: options);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenMaxNestingDepthIs2_AndNestedObjectCreationTwoLevelsDeep()
    {
        var options = new Dictionary<string, string>
        {
            [NestedCallAnalyzer.MaxNestingDepthConfigKey] = "2",
        };

        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            Install(new Wrapper(new Leaf()));
        }

        private void Install(Wrapper w) { }
    }

    public class Leaf { }

    public class Wrapper
    {
        public Wrapper(Leaf l) { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new NestedCallAnalyzer(),
            NestedCallAnalyzer.DiagnosticId,
            analyzerOptions: options);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenMaxNestingDepthIs2_AndCallChainTwoLevelsDeep()
    {
        var options = new Dictionary<string, string>
        {
            [NestedCallAnalyzer.MaxNestingDepthConfigKey] = "2",
        };

        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            Outer(Inner(GetValue()));
        }

        private void Outer(int x) { }
        private int Inner(int v) => v;
        private int GetValue() => 1;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new NestedCallAnalyzer(),
            NestedCallAnalyzer.DiagnosticId,
            analyzerOptions: options);

        Assert.Single(diagnostics);
    }
}
