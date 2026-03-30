using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class ScriptPathFiltersTests
{
    [Fact]
    public void GetFileName_TreatsBackslashAsSeparator_OnSyntheticWindowsPaths()
    {
        const string path = @"C:\Repo\Assets\Scripts\Core\GameEngine\Runtime\Game.cs";
        Assert.Equal("Game.cs", ScriptPathFilters.GetFileName(path));
    }

    [Fact]
    public void GetFileNameWithoutExtension_StripsExtension_AfterBackslashNormalization()
    {
        const string path = @"C:\Repo\Assets\Scripts\Core\Sample.cs";
        Assert.Equal("Sample", ScriptPathFilters.GetFileNameWithoutExtension(path));
    }

    [Fact]
    public void IsUnderAssetsScripts_MatchesRelativeUnityPath_WithoutLeadingSlash()
    {
        const string path = "Assets/Scripts/Infra/MVVM/Runtime/BindContext.cs";
        Assert.True(ScriptPathFilters.IsUnderAssetsScripts(ScriptPathFilters.Normalize(path)));
    }

    [Fact]
    public void IsUnderAssetsScripts_MatchesAbsoluteStylePath_WithLeadingSlashSegment()
    {
        const string path = "/repo/Assets/Scripts/Core/Game.cs";
        Assert.True(ScriptPathFilters.IsUnderAssetsScripts(ScriptPathFilters.Normalize(path)));
    }

    [Fact]
    public void TryGetPathAfterAssetsScripts_ReturnsRemainder_AfterRelativePrefix()
    {
        const string path = "Assets/Scripts/App/View/Runtime/UIView.cs";
        Assert.True(ScriptPathFilters.TryGetPathAfterAssetsScripts(ScriptPathFilters.Normalize(path), out var remainder));
        Assert.Equal("App/View/Runtime/UIView.cs", remainder);
    }
}
