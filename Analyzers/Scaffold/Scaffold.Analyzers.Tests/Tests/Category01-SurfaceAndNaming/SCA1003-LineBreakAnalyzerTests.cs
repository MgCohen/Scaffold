using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class LineBreakAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenMethodSignatureSpansMultipleLines()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute(
            string value)
        {
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_ForFluentInvocationAcrossLines()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            builder
                .WithName(""n"")
                .Build();
        }

        private Builder builder = new Builder();
    }

    public class Builder
    {
        public Builder WithName(string value) { return this; }
        public Builder Build() { return this; }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenConstructorSignatureSpansMultipleLines()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public Sample(
            string value)
        {
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenConstructorInitializerStartsOnNewLine()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public Sample(string value)
            : base(value)
        {
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenConstructorInitializerArgumentsSpanMultipleLines()
    {
        const string source = @"
namespace Demo
{
    public class Sample : Base
    {
        public Sample(string value) : base(
            value)
        {
        }
    }

    public class Base
    {
        protected Base(string value)
        {
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenConstructorInitializerIsSingleLine()
    {
        const string source = @"
namespace Demo
{
    public class Sample : Base
    {
        public Sample(string value) : base(value) { }
        public Sample() : base() { }
    }

    public class Base
    {
        protected Base() { }
        protected Base(string value) { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenMethodWhereClauseSpansMultipleLines()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute<T>(T value)
            where T : class
        {
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenInterfaceWhereClauseSpansMultipleLines()
    {
        const string source = @"
namespace Demo
{
    public interface IEntityBehavior<TData, TInput>
        where TData : class
    {
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenClassGenericTypeParameterListSpansMultipleLines()
    {
        const string source = @"
namespace Demo
{
    public class Sample<
        TData,
        TInput>
    {
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_ForSingleLineGenericInterfaceWithConstraints()
    {
        const string source = @"
namespace Demo
{
    public interface IEntityBehavior<TData, TInput> where TData : class
    {
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_ForMultilineCollectionInitializerLocalDeclaration()
    {
        const string source = @"
using System.Collections.Generic;

namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            List<int> values = new List<int>
            {
                1,
                2
            };
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_ForMultilineSimpleLocalDeclaration()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            int value =
                1;
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_ForMultilineObjectInitializerAssignmentStatement()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            Item item = null;
            item = new Item
            {
                Name = ""ok""
            };
        }
    }

    public class Item
    {
        public string Name { get; set; }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenTypeAttributeSharesLineWithClassKeyword()
    {
        const string source = @"
namespace Demo
{
    [Serializable] public class Sample<T> where T : class
    {
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
        Assert.Contains("Place type attributes on separate lines", diagnostics[0].GetMessage());
    }

    [Fact]
    public async Task NoDiagnostic_WhenTypeAttributesAreAboveClassDeclaration()
    {
        const string source = @"
namespace Demo
{
    [Serializable]
    public class Sample<T> where T : class
    {
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenMethodSignatureSpansMultipleLines_WithAttributeOnSeparateLine()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        [Obsolete]
        public void Execute(
            string value)
        {
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
        Assert.Contains("Collapse the statement back onto a single line", diagnostics[0].GetMessage());
    }

    [Fact]
    public async Task Diagnostic_WhenSwitchExpressionIsSingleLineWithMultipleArms()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            var x = 1 switch { 1 => 2, _ => 3 };
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
        Assert.Contains("opening brace on the line after", diagnostics[0].GetMessage());
    }

    [Fact]
    public async Task NoDiagnostic_WhenSwitchExpressionIsMultiline()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            var x = 1 switch
            {
                1 => 2,
                _ => 3
            };
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenSwitchStatementBraceOnSameLineAsSwitch()
    {
        const string source = @"
namespace Demo
{
    public class Sample
    {
        public void Execute(int x)
        {
            switch (x) { case 1: break; default: break; }
        }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            new LineBreakAnalyzer(),
            LineBreakAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
        Assert.Contains("switch statements", diagnostics[0].GetMessage());
    }
}
