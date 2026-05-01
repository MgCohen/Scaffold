using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class SerializableBackingFieldAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenSerializableClassHasPublicGetterPrivateSetterWithoutSerializeFieldPattern()
    {
        const string source = @"
using System;
namespace Scaffold.Navigation
{
    [Serializable]
    public class ViewModel
    {
        public int Count { get; private set; }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Runtime\ViewModel.cs",
            new SerializableBackingFieldAnalyzer(),
            SerializableBackingFieldAnalyzer.DiagnosticId,
            includeUnityEngineReference: true);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenSerializableClassHasPublicAutoPropertyWithPublicSetter()
    {
        const string source = @"
using System;
namespace Scaffold.Navigation
{
    [Serializable]
    public class ViewModel
    {
        public int Count { get; set; }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Runtime\ViewModel.cs",
            new SerializableBackingFieldAnalyzer(),
            SerializableBackingFieldAnalyzer.DiagnosticId,
            includeUnityEngineReference: true);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenSerializableClassHasGetterOnlyAutoProperty()
    {
        const string source = @"
using System;
namespace Scaffold.Navigation
{
    [Serializable]
    public class ViewModel
    {
        public int Count { get; }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Runtime\ViewModel.cs",
            new SerializableBackingFieldAnalyzer(),
            SerializableBackingFieldAnalyzer.DiagnosticId,
            includeUnityEngineReference: true);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenSerializableClassUsesSerializeFieldBackingFieldAndGetter()
    {
        const string source = @"
using System;
using UnityEngine;
namespace Scaffold.Navigation
{
    [Serializable]
    public class ViewModel
    {
        [SerializeField] private int count;
        public int Count => count;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Runtime\ViewModel.cs",
            new SerializableBackingFieldAnalyzer(),
            SerializableBackingFieldAnalyzer.DiagnosticId,
            includeUnityEngineReference: true);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenCompilationIsNotUnityFacing()
    {
        const string source = @"
using System;
namespace Scaffold.Meta.Level
{
    [Serializable]
    public class LevelModel
    {
        public int Index { get; private set; }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Scaffold\Meta\Level\Runtime\LevelModel.cs",
            new SerializableBackingFieldAnalyzer(),
            SerializableBackingFieldAnalyzer.DiagnosticId,
            includeUnityEngineReference: false);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenPathIsTests()
    {
        const string source = @"
using System;
namespace Scaffold.Navigation.Tests
{
    [Serializable]
    public class ViewModel
    {
        public int Count { get; private set; }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Tests\ViewModelTests.cs",
            new SerializableBackingFieldAnalyzer(),
            SerializableBackingFieldAnalyzer.DiagnosticId,
            includeUnityEngineReference: true);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenMonoBehaviourHasPublicGetterPrivateSetterWithoutSerializeFieldPattern()
    {
        const string source = @"
using UnityEngine;
namespace Scaffold.App
{
    public sealed class MainView : MonoBehaviour
    {
        public int Count { get; private set; }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\App\MainMenu\Runtime\MainView.cs",
            new SerializableBackingFieldAnalyzer(),
            SerializableBackingFieldAnalyzer.DiagnosticId,
            includeUnityEngineReference: true);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenMonoBehaviourUsesSerializeFieldBackingFieldAndGetter()
    {
        const string source = @"
using UnityEngine;
namespace Scaffold.App
{
    public sealed class MainView : MonoBehaviour
    {
        [SerializeField] private int count;
        public int Count => count;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\App\MainMenu\Runtime\MainView.cs",
            new SerializableBackingFieldAnalyzer(),
            SerializableBackingFieldAnalyzer.DiagnosticId,
            includeUnityEngineReference: true);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenClassIsNotSerializableAndDoesNotInheritUnityObject()
    {
        const string source = @"
namespace Scaffold.Core
{
    public sealed class PlainDto
    {
        public int Value { get; private set; }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Runtime\PlainDto.cs",
            new SerializableBackingFieldAnalyzer(),
            SerializableBackingFieldAnalyzer.DiagnosticId,
            includeUnityEngineReference: true);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenScriptableObjectHasPublicGetterPrivateSetterWithoutSerializeFieldPattern()
    {
        const string source = @"
using UnityEngine;
namespace Scaffold.Data
{
    public sealed class GameConfig : ScriptableObject
    {
        public int MaxLevel { get; private set; }
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Data\Runtime\GameConfig.cs",
            new SerializableBackingFieldAnalyzer(),
            SerializableBackingFieldAnalyzer.DiagnosticId,
            includeUnityEngineReference: true);

        Assert.Single(diagnostics);
    }
}
