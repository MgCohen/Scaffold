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

    [Fact]
    public void IsUnderAssetsPackages_MatchesEmbeddedPackagePath()
    {
        const string path = "Assets/Packages/com.scaffold.cloudcode/Runtime/Optimistic/IRequestHandler.cs";
        Assert.True(ScriptPathFilters.IsUnderAssetsPackages(ScriptPathFilters.Normalize(path)));
    }

    [Fact]
    public void IsUnderAssetsPackagesComScaffold_True_ForFirstPartyEmbeddedPackage()
    {
        const string path = "Assets/Packages/com.scaffold.maps/Runtime/Map.cs";
        Assert.True(ScriptPathFilters.IsUnderAssetsPackagesComScaffold(ScriptPathFilters.Normalize(path)));
    }

    [Fact]
    public void IsUnderAssetsPackagesComScaffold_True_WhenPathHasDrivePrefix()
    {
        const string path = @"C:\Repo\Assets\Packages\com.scaffold.states\Runtime\Store.cs";
        Assert.True(ScriptPathFilters.IsUnderAssetsPackagesComScaffold(ScriptPathFilters.Normalize(path)));
    }

    [Fact]
    public void IsUnderAssetsPackagesComScaffold_False_ForOtherEmbeddedPackages()
    {
        const string path = "Assets/Packages/com.unity.somepackage/Runtime/Foo.cs";
        Assert.False(ScriptPathFilters.IsUnderAssetsPackagesComScaffold(ScriptPathFilters.Normalize(path)));
    }

    [Fact]
    public void TryGetPathAfterAssetsPackages_ReturnsRemainder_AfterRelativePrefix()
    {
        const string path = "Assets/Packages/com.scaffold.cloudcode/Runtime/CloudCodeService.cs";
        Assert.True(ScriptPathFilters.TryGetPathAfterAssetsPackages(ScriptPathFilters.Normalize(path), out var remainder));
        Assert.Equal("com.scaffold.cloudcode/Runtime/CloudCodeService.cs", remainder);
    }

    [Fact]
    public void IsUnderAssetsScriptsOrPackages_True_ForEitherRoot()
    {
        Assert.True(ScriptPathFilters.IsUnderAssetsScriptsOrPackages("Assets/Scripts/Core/Game.cs"));
        Assert.True(ScriptPathFilters.IsUnderAssetsScriptsOrPackages("Assets/Packages/com.foo/Runtime/Bar.cs"));
        Assert.False(ScriptPathFilters.IsUnderAssetsScriptsOrPackages("Assets/Plugins/SomePlugin/Foo.cs"));
    }

    [Fact]
    public void IsUnityScriptPath_True_ForPackagesRuntime_WhenNotTestOrGenerated()
    {
        const string path = "Assets/Packages/com.scaffold.cloudcode/Runtime/Optimistic/IRequestHandler.cs";
        Assert.True(ScriptPathFilters.IsUnityScriptPath(path));
    }
}
