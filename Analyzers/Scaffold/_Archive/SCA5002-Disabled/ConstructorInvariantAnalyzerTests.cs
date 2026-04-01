using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class ConstructorInvariantAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenTypeIsMentionedByNonSiblingRuntimeAssembly()
    {
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            CreateExternalConsumerGraph(CreateConstructorSource(withValidation: false), "private Scaffold.Model.Widget widget;"),
            new ConstructorInvariantAnalyzer(),
            ConstructorInvariantAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenExternallyUsedTypeHasLeadingGuard()
    {
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            CreateExternalConsumerGraph(CreateConstructorSource(withValidation: true), "private Scaffold.Model.Widget widget;"),
            new ConstructorInvariantAnalyzer(),
            ConstructorInvariantAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenTypeIsNotExternallyMentioned()
    {
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            CreateExternalConsumerGraph(CreateConstructorSource(withValidation: false), string.Empty),
            new ConstructorInvariantAnalyzer(),
            ConstructorInvariantAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenExternallyMentionedInterfaceHasImplementation()
    {
        const string source = @"
namespace Scaffold.Model
{
    public interface IWidgetApi
    {
        void Execute(string input);
    }

    public sealed class Widget : IWidgetApi
    {
        public Widget(object dependency)
        {
            this.Dependency = dependency;
        }

        public object Dependency { get; }
        public void Execute(string input) { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            CreateExternalConsumerGraph(source, "private Scaffold.Model.IWidgetApi api;"),
            new ConstructorInvariantAnalyzer(),
            ConstructorInvariantAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    private static string CreateConstructorSource(bool withValidation)
    {
        if (withValidation)
        {
            return @"
namespace Scaffold.Model
{
    public sealed class Widget
    {
        public Widget(object dependency)
        {
            System.ArgumentNullException.ThrowIfNull(dependency);
            this.Dependency = dependency;
        }

        public object Dependency { get; }
    }
}";
        }

        return @"
namespace Scaffold.Model
{
    public sealed class Widget
    {
        public Widget(object dependency)
        {
            this.Dependency = dependency;
        }

        public object Dependency { get; }
    }
}";
    }

    private static StructuralTestGraph CreateExternalConsumerGraph(string modelSource, string consumerField)
    {
        var consumerSource = string.IsNullOrWhiteSpace(consumerField)
            ? "namespace Scaffold.Gameplay { public sealed class Usage { } }"
            : "namespace Scaffold.Gameplay { public sealed class Usage { " + consumerField + " } }";

        return StructuralTestGraph
            .Create("Scaffold.Model")
            .Assembly("Scaffold.Model")
                .WithSource("Assets/Scripts/Core/Model/Runtime/Widget.cs", modelSource)
            .Assembly("Scaffold.Gameplay")
                .WithSource("Assets/Scripts/Core/Gameplay/Runtime/Usage.cs", consumerSource)
                .References("Scaffold.Model")
            .Build();
    }
}
