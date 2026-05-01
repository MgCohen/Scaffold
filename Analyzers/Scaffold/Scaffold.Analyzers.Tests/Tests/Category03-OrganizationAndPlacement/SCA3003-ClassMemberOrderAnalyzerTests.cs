using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class ClassMemberOrderAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenConstructorAppearsAfterProperty()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public int Value => 1;
        public Sample() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new ClassMemberOrderAnalyzer(),
            ClassMemberOrderAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenStaticPropertyIsBeforeConstructor()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public static int Version => 1;
        public Sample() { }
        public int this[int index] => index;
        public int Value => value;
        private int value;
        public event System.Action Changed;
        public void Tick() { }
        public static void Reset() { }
        private class Nested { }
        public static Sample operator +(Sample a, Sample b) => a;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new ClassMemberOrderAnalyzer(),
            ClassMemberOrderAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenConstFieldIsBeforeConstructor()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        private const int DefaultValue = 1;
        public Sample() { }
        public int Value => DefaultValue;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new ClassMemberOrderAnalyzer(),
            ClassMemberOrderAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenBackingFieldDirectlyFollowsProperty()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public Sample() { }
        public int MyValue => myValue;
        private int myValue;
        public int AnotherValue => anotherValue;
        private int anotherValue;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new ClassMemberOrderAnalyzer(),
            ClassMemberOrderAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenStaticMethodAppearsBeforeInstanceMethod()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public Sample() { }
        public static void B() { }
        public void A() { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new ClassMemberOrderAnalyzer(),
            ClassMemberOrderAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }
}
