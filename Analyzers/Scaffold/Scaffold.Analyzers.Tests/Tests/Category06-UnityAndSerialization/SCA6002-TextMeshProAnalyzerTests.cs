using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class TextMeshProUsageAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenUnityTextTypeIsUsed()
    {
        const string source = @"
namespace UnityEngine.UI
{
    public class Text { }
}

namespace Sample
{
    using UnityEngine.UI;

    public class MainMenuView
    {
        private Text currentLabel;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\App\MainMenu\Runtime\MainMenuView.cs",
            new TextMeshProUsageAnalyzer(),
            TextMeshProUsageAnalyzer.DiagnosticId);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("UnityEngine.UI.Text", diagnostic.GetMessage());
        Assert.Contains("TMPro.TextMeshProUGUI", diagnostic.GetMessage());
    }

    [Fact]
    public async Task NoDiagnostic_WhenTextMeshProIsUsed()
    {
        const string source = @"
namespace UnityEngine.UI
{
    public class Text { }
}

namespace TMPro
{
    public class TextMeshProUGUI { }
}

namespace Sample
{
    using TMPro;

    public class MainMenuView
    {
        private TextMeshProUGUI currentLabel;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\App\MainMenu\Runtime\MainMenuView.cs",
            new TextMeshProUsageAnalyzer(),
            TextMeshProUsageAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenDifferentTextTypeIsUsed()
    {
        const string source = @"
namespace MyApp.UI
{
    public class Text { }
}

namespace Sample
{
    using MyApp.UI;

    public class MainMenuView
    {
        private Text currentLabel;
    }
}";

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\App\MainMenu\Runtime\MainMenuView.cs",
            new TextMeshProUsageAnalyzer(),
            TextMeshProUsageAnalyzer.DiagnosticId);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenCustomForbiddenTypeConfigured()
    {
        const string source = @"
namespace Legacy.UI
{
    public class OldLabel { }
}

namespace Sample
{
    using Legacy.UI;

    public class MainMenuView
    {
        private OldLabel currentLabel;
    }
}";

        var options = new System.Collections.Generic.Dictionary<string, string>
        {
            ["scaffold.SCA6002.forbidden_types"] = "Legacy.UI.OldLabel=>TMPro.TextMeshProUGUI",
        };

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\Assets\Scripts\App\MainMenu\Runtime\MainMenuView.cs",
            new TextMeshProUsageAnalyzer(),
            TextMeshProUsageAnalyzer.DiagnosticId,
            options);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("Legacy.UI.OldLabel", diagnostic.GetMessage());
        Assert.Contains("TMPro.TextMeshProUGUI", diagnostic.GetMessage());
    }
}
