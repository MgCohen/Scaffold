using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class NamespaceLayoutAnalyzerTests
{
    /// <summary>Defaults aligned with repo .editorconfig (SCA3005/SCA3006); tests override as needed.</summary>
    private static Dictionary<string, string> MergeNamespaceOptions(Dictionary<string, string>? extra = null)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["scaffold.SCA3005.root"] = "Scaffold",
            ["scaffold.SCA3005.allowed_roots"] = "GameModule;GameModuleDTO;Scaffold",
            ["scaffold.SCA3006.content_roots"] = "Assets/Scripts;LiveOps/Project",
        };

        if (extra != null)
        {
            foreach (var kv in extra)
            {
                d[kv.Key] = kv.Value;
            }
        }

        return d;
    }

    [Fact]
    public async Task NoDiagnostic_WhenNamespaceMatchesFolderPath()
    {
        const string source = @"
namespace Scaffold.Events
{
    public class EventBus { }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Events\EventBus.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenNamespaceDoesNotMatchFolderPath_ReportsSca3005AndSca3006()
    {
        const string source = @"
namespace Utilities.Navigation
{
    public class EventBus { }
}";

        var all = await AnalyzerTestHarness.GetDiagnosticsAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Events\EventBus.cs",
            NamespaceLayoutTestAnalyzers.RootAndPath,
            MergeNamespaceOptions());

        var d5 = all.Where(d => d.Id == NamespaceRootAnalyzer.DiagnosticId).ToArray();
        var d6 = all.Where(d => d.Id == NamespacePathAnalyzer.DiagnosticId).ToArray();
        Assert.Single(d5);
        Assert.Single(d6);
        Assert.Contains("Utilities", d5[0].GetMessage());
        Assert.Contains("Scaffold.Events", d6[0].GetMessage());
    }

    [Fact]
    public async Task Diagnostic_WhenNoNamespaceDeclared_ReportsSca3005()
    {
        const string source = @"
public class X
{
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Events\EventBus.cs",
            new NamespaceRootAnalyzer(),
            NamespaceRootAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("no namespace declaration", diagnostic.GetMessage(), System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Diagnostic_WhenNamespaceMissing()
    {
        const string source = @"
public class EventBus
{
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Events\EventBus.cs",
            new NamespaceRootAnalyzer(),
            NamespaceRootAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("no namespace declaration", diagnostic.GetMessage(), System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NoDiagnostic_WhenFileContainsOnlyAssemblyAttributes()
    {
        const string source = @"
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(""Scaffold.Addressables.Tests"")]";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Addressables\Runtime\AssemblyInfo.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenAssemblyInfoContainsCodeWithoutNamespace()
    {
        const string source = @"
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(""Scaffold.Addressables.Tests"")]
internal static class Marker
{
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Addressables\Runtime\AssemblyInfo.cs",
            new NamespaceRootAnalyzer(),
            NamespaceRootAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("no namespace declaration", diagnostic.GetMessage(), System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NoDiagnostic_ForFilesOutsideAssetsScripts()
    {
        const string source = @"
namespace NotScaffold
{
    public class EventBus { }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Docs\EventBus.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenCustomRootNamespaceConfigured()
    {
        const string source = @"
namespace Custom.Events
{
    public class EventBus { }
}";

        var options = MergeNamespaceOptions(new Dictionary<string, string>
        {
            ["scaffold.SCA3005.root"] = "Custom",
            ["scaffold.SCA3005.allowed_roots"] = "Custom",
        });

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Events\EventBus.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            options);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenImportedRootAndDomainOmittedFromNamespace()
    {
        const string source = @"
namespace Scaffold.Navigation.Container
{
    public class NavigationInstaller { }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Container\NavigationInstaller.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenRuntimeAndImplementationSegmentsAreSkipped()
    {
        const string source = @"
namespace Scaffold.MVVM.Binding
{
    public class BindSet { }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\MVVM\Runtime\Binding\Implementation\BindSet.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenRuntimeSegmentIsSkippedAndContractsIsKept()
    {
        const string source = @"
namespace Scaffold.Navigation.Contracts
{
    public interface INavigation { }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Runtime\Contracts\INavigation.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenTopLevelContractsFolderMapsToContractsNamespace()
    {
        const string source = @"
namespace Scaffold.Navigation.Contracts
{
    public interface INavigation { }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Contracts\INavigation.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenNamespaceIncludesSkippedRuntimeSegment()
    {
        const string source = @"
namespace Scaffold.Navigation.Runtime.Contracts
{
    public interface INavigation { }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Runtime\Contracts\INavigation.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("Scaffold.Navigation.Contracts", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Diagnostic_WhenTopLevelContractsNamespaceIsMissingContractsSegment()
    {
        const string source = @"
namespace Scaffold.Navigation
{
    public interface INavigation { }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Contracts\INavigation.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("Scaffold.Navigation.Contracts", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Diagnostic_WhenSecondTopLevelNamespaceDoesNotMatchFolderPath()
    {
        const string source = @"
namespace Scaffold.Navigation.Contracts
{
    public interface INavigation { }
}

namespace Scaffold.Navigation
{
    public class NavigationImpl { }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Contracts\NavigationImpl.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("Scaffold.Navigation", diagnostic.GetMessage());
        Assert.Contains("Scaffold.Navigation.Contracts", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Diagnostic_WhenTypeDeclaredAtFileScopeOutsideBlockNamespace()
    {
        const string source = @"
namespace Scaffold.Navigation.Contracts
{
    public interface INavigation { }
}

public class Stray { }
";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Contracts\INavigation.cs",
            new SingleTopLevelNamespaceAnalyzer(),
            SingleTopLevelNamespaceAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("Stray", diagnostic.GetMessage());
        Assert.Contains("file scope outside the namespace block", diagnostic.GetMessage());
    }

    [Fact]
    public async Task NoDiagnostic_WhenFileScopedNamespaceOnly_TypesUnderNamespace()
    {
        const string source = @"
namespace Scaffold.Infra.Events;

public class EventBus { }
";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Events\EventBus.cs",
            new SingleTopLevelNamespaceAnalyzer(),
            SingleTopLevelNamespaceAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenFileContainsMultipleTopLevelNamespaces()
    {
        const string source = @"
namespace Scaffold.Navigation.Contracts
{
    public interface INavigation { }
}

namespace Scaffold.Navigation.Contracts
{
    public interface IRoute { }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Contracts\INavigation.cs",
            new SingleTopLevelNamespaceAnalyzer(),
            SingleTopLevelNamespaceAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("2 top-level namespace declarations", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Diagnostic_WhenAnyTopLevelNamespaceDoesNotMatchExpectedSuffix()
    {
        const string source = @"
namespace Scaffold.Navigation.Contracts
{
    public interface INavigation { }
}

namespace Wrong.Namespace
{
    public class NavigationImpl { }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Contracts\NavigationImpl.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("Wrong.Namespace", diagnostic.GetMessage());
    }

    [Fact]
    public async Task NoDiagnostic_ForIsExternalInitExemptFile()
    {
        const string source = @"
namespace Scaffold.Records
{
}

namespace System.Runtime.CompilerServices
{
    public static class IsExternalInit { }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Tools\Records\Runtime\IsExternalInit.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_ForIsExternalInitExemptFile_OnMultipleNamespaceRule()
    {
        const string source = @"
namespace Scaffold.Records
{
}

namespace System.Runtime.CompilerServices
{
    public static class IsExternalInit { }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Tools\Records\Runtime\IsExternalInit.cs",
            new SingleTopLevelNamespaceAnalyzer(),
            SingleTopLevelNamespaceAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenUsingAlternateContentRoot()
    {
        const string source = @"
namespace Scaffold
{
    public class Foo { }
}";

        var options = MergeNamespaceOptions(new Dictionary<string, string>
        {
            ["scaffold.SCA3006.content_roots"] = "Assets/SharedScripts",
        });

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\SharedScripts\Foo.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            options);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenFileUnderLiveOpsProjectContentRoot()
    {
        const string source = @"
namespace GameModule.Modules.Level
{
    public class LevelService { }
}";

        // Key present but empty => do not apply legacy "skip first folder segment" (path is .../Modules/Level, not a single segment).
        var options = MergeNamespaceOptions(new Dictionary<string, string>
        {
            ["scaffold.SCA3006.first_segment_ignore"] = string.Empty,
        });

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\LiveOps\Project\Modules\Level\LevelService.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            options);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNamespaceMatchesPathWithScaffoldRoot()
    {
        const string source = @"
namespace Scaffold.Core
{
    public class Foo { }
}";

        // Key present but empty => keep "Core" in the folder-derived suffix (legacy skip would drop it).
        var options = MergeNamespaceOptions(new Dictionary<string, string>
        {
            ["scaffold.SCA3006.first_segment_ignore"] = string.Empty,
        });

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Core\Foo.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            options);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenFirstPathSegmentInfraIsIgnoredByConfig()
    {
        const string source = @"
namespace Scaffold
{
    public class Code { }
}";

        var options = MergeNamespaceOptions(new Dictionary<string, string>
        {
            ["scaffold.SCA3006.first_segment_ignore"] = "Infra",
        });

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Code.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            options);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenWrongRootButFolderSuffixMatches_ReportsSca3006()
    {
        const string source = @"
namespace WrongCorp.Events
{
    public class EventBus { }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Events\EventBus.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            MergeNamespaceOptions());

        var diagnostic = Assert.Single(diagnostics);
        var message = diagnostic.GetMessage();
        Assert.Contains("Scaffold.Events", message);
        Assert.Contains("WrongCorp.Events", message);
    }

    [Fact]
    public async Task NoDiagnostic_WhenAllowedRootsIncludesAlternateFirstSegment()
    {
        const string source = @"
namespace ExtraCorp.Events
{
    public class EventBus { }
}";

        var options = MergeNamespaceOptions(new Dictionary<string, string>
        {
            ["scaffold.SCA3005.allowed_roots"] = "ExtraCorp;Scaffold;GameModule;GameModuleDTO",
        });

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Events\EventBus.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            options);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenExplicitCleanupProjectRoot_Configured()
    {
        const string source = @"
namespace Utilities.Navigation
{
    public class EventBus { }
}";

        var options = MergeNamespaceOptions(new Dictionary<string, string>
        {
            ["scaffold.SCA3005.root"] = "CleanupProject",
            ["scaffold.SCA3005.allowed_roots"] = "CleanupProject",
        });

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\Events\EventBus.cs",
            new NamespacePathAnalyzer(),
            NamespacePathAnalyzer.DiagnosticId,
            options);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("CleanupProject.Events", diagnostic.GetMessage());
    }
}
