using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class InvariantEntryPointAnalyzerTests
{
    [Fact]
    public async Task NoDiagnostic_WhenTypeIsNotMentionedByExternalAssembly()
    {
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            CreateExternalConsumerGraph(CreateWidgetSource(), string.Empty),
            new InvariantEntryPointAnalyzer(),
            InvariantEntryPointAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenTypeIsMentionedByNonSiblingRuntimeAssembly()
    {
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            CreateExternalConsumerGraph(CreateWidgetSource(), "private Scaffold.Model.Widget widget;"),
            new InvariantEntryPointAnalyzer(),
            InvariantEntryPointAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenOnlySiblingTestsMentionType()
    {
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            StructuralTestGraph
                .Create("Scaffold.Model")
                .Assembly("Scaffold.Model")
                    .WithSource("Assets/Scripts/Core/Model/Runtime/Widget.cs", CreateWidgetSource())
                .Assembly("Scaffold.Model.Tests")
                    .WithSource(
                        "Assets/Scripts/Core/Model/Tests/WidgetTests.cs",
                        "namespace Scaffold.Model.Tests { public sealed class WidgetTests { private Scaffold.Model.Widget widget; } }")
                    .References("Scaffold.Model")
                .Build(),
            new InvariantEntryPointAnalyzer(),
            InvariantEntryPointAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenSiblingContainerMentionsType()
    {
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            StructuralTestGraph
                .Create("Scaffold.Model")
                .Assembly("Scaffold.Model")
                    .WithSource("Assets/Scripts/Core/Model/Runtime/Widget.cs", CreateWidgetSource())
                .Assembly("Scaffold.Model.Container")
                    .WithSource(
                        "Assets/Scripts/Core/Model/Container/Installer.cs",
                        "namespace Scaffold.Model.Container { public sealed class Installer { private Scaffold.Model.Widget widget; } }")
                    .References("Scaffold.Model")
                .Build(),
            new InvariantEntryPointAnalyzer(),
            InvariantEntryPointAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
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
        public void Execute(string input)
        {
            Consume(input);
        }

        private void Consume(string input) { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            CreateExternalConsumerGraph(source, "private Scaffold.Model.IWidgetApi api;"),
            new InvariantEntryPointAnalyzer(),
            InvariantEntryPointAnalyzer.DiagnosticId);

        Assert.Single(diagnostics);
    }

    private static string CreateWidgetSource()
    {
        return @"
namespace Scaffold.Model
{
    public sealed class Widget
    {
        public void Execute(string input)
        {
            Consume(input);
        }

        private void Consume(string input) { }
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

    [Fact]
    public async Task NoDiagnostic_ForUnityEventSystemInterfaceImplementations()
    {
        const string source = @"
namespace UnityEngine.EventSystems
{
    public class PointerEventData {}

    public interface IPointerClickHandler
    {
        void OnPointerClick(PointerEventData eventData);
    }
}

namespace Scaffold.App.GameView
{
    using UnityEngine.EventSystems;

    public class VirtualJoystickInput : IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            Handle(eventData);
        }

        private void Handle(PointerEventData eventData) { }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\App\MainMenu\Runtime\MainMenuView.cs",
            new InvariantEntryPointAnalyzer(),
            InvariantEntryPointAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }
}
