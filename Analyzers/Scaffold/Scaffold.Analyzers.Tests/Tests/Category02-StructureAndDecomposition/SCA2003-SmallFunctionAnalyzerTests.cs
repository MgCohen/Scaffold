using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class SmallFunctionAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenMethodExceedsConfiguredMaxLines_Fifteen()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            Step1();
            Step2();
            Step3();
            Step4();
            Step5();
            Step6();
            Step7();
            Step8();
            Step9();
            Step10();
            Step11();
            Step12();
            Step13();
            Step14();
            Step15();
            Step16();
        }

        private void Step1() { }
        private void Step2() { }
        private void Step3() { }
        private void Step4() { }
        private void Step5() { }
        private void Step6() { }
        private void Step7() { }
        private void Step8() { }
        private void Step9() { }
        private void Step10() { }
        private void Step11() { }
        private void Step12() { }
        private void Step13() { }
        private void Step14() { }
        private void Step15() { }
        private void Step16() { }
    }
}";

        var options = new Dictionary<string, string>
        {
            ["scaffold.SCA2003.max_lines"] = "15",
        };

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new SmallFunctionAnalyzer(),
            SmallFunctionAnalyzer.DiagnosticId,
            options);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenMaxLinesNotConfigured()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            Step1();
            Step2();
            Step3();
            Step4();
            Step5();
            Step6();
            Step7();
            Step8();
            Step9();
            Step10();
            Step11();
            Step12();
            Step13();
            Step14();
            Step15();
            Step16();
        }

        private void Step1() { }
        private void Step2() { }
        private void Step3() { }
        private void Step4() { }
        private void Step5() { }
        private void Step6() { }
        private void Step7() { }
        private void Step8() { }
        private void Step9() { }
        private void Step10() { }
        private void Step11() { }
        private void Step12() { }
        private void Step13() { }
        private void Step14() { }
        private void Step15() { }
        private void Step16() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new SmallFunctionAnalyzer(),
            SmallFunctionAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenMethodExceedsConfiguredMaxLines()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            Step1();
            Step2();
            Step3();
            Step4();
            Step5();
            Step6();
            Step7();
            Step8();
            Step9();
            Step10();
            Step11();
            Step12();
            Step13();
        }

        private void Step1() { }
        private void Step2() { }
        private void Step3() { }
        private void Step4() { }
        private void Step5() { }
        private void Step6() { }
        private void Step7() { }
        private void Step8() { }
        private void Step9() { }
        private void Step10() { }
        private void Step11() { }
        private void Step12() { }
        private void Step13() { }
    }
}";

        var options = new Dictionary<string, string>
        {
            ["scaffold.SCA2003.max_lines"] = "12",
        };

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new SmallFunctionAnalyzer(),
            SmallFunctionAnalyzer.DiagnosticId,
            options);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenMethodHasTwelveNonEmptyLinesWithBlankLines()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            Step1();
            Step2();

            Step3();
            Step4();
            Step5();
            Step6();

            Step7();
            Step8();
            Step9();
            Step10();
            Step11();
            Step12();
        }

        private void Step1() { }
        private void Step2() { }
        private void Step3() { }
        private void Step4() { }
        private void Step5() { }
        private void Step6() { }
        private void Step7() { }
        private void Step8() { }
        private void Step9() { }
        private void Step10() { }
        private void Step11() { }
        private void Step12() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new SmallFunctionAnalyzer(),
            SmallFunctionAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenFluentContinuationLinesDoNotCountTowardLimit()
    {
        const string source = @"
namespace Demo
{
    public sealed class Chain
    {
        public Chain Dot() => this;
    }

    public class Sample
    {
        public void Execute()
        {
            var a = 0;
            var b = new Chain()
                .Dot()
                .Dot()
                .Dot()
                .Dot()
                .Dot()
                .Dot()
                .Dot()
                .Dot()
                .Dot()
                .Dot()
                .Dot()
                .Dot()
                .Dot()
                .Dot()
                .Dot()
                .Dot()
                .Dot()
                .Dot()
                .Dot()
                .Dot();
        }
    }
}";

        var options = new Dictionary<string, string>
        {
            ["scaffold.SCA2003.max_lines"] = "2",
        };

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new SmallFunctionAnalyzer(),
            SmallFunctionAnalyzer.DiagnosticId,
            options);

        Assert.Empty(diagnostics);
    }
}
