using System.Threading.Tasks;
using Scaffold.Analyzers;
using Xunit;

namespace Scaffold.Analyzers.Tests;

/// <summary>
/// One baseline per SCA rule: <c>TestData/Golden/{name}.cs</c> + <c>{name}.golden.txt</c>.
/// </summary>
public sealed class ScaRuleGoldenTests
{
    [Fact]
    public Task SCA1001_MethodComment() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA1001-MethodComment",
            new DeclarationCommentAnalyzer(),
            @"C:\Repo\Assets\Scripts\Core\Sample.cs");

    [Fact]
    public Task SCA2001_MethodOrder() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA2001-MethodOrder",
            new MethodOrderAnalyzer(),
            @"C:\Repo\Assets\Scripts\Core\Sample.cs");

    [Fact]
    public Task SCA2002_NestedCall() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA2002-NestedCall",
            new NestedCallAnalyzer(),
            @"C:\Repo\Assets\Scripts\Core\Sample.cs");

    [Fact]
    public Task SCA1002_ExpressionBody() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA1002-ExpressionBody",
            new ExpressionBodiedMethodAnalyzer(),
            @"C:\Repo\Assets\Scripts\Core\Sample.cs");

    [Fact]
    public Task SCA1003_LineBreak() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA1003-LineBreak",
            new LineBreakAnalyzer(),
            @"C:\Repo\Assets\Scripts\Core\Sample.cs");

    [Fact]
    public Task SCA2003_SmallFunction() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA2003-SmallFunction",
            new SmallFunctionAnalyzer(),
            @"C:\Repo\Assets\Scripts\Core\Sample.cs");

    [Fact]
    public Task SCA3006_Namespace() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA3006-Namespace",
            new NamespacePathAnalyzer(),
            @"C:\Repo\Assets\Scripts\Infra\Events\EventBus.cs",
            NamespacePathAnalyzer.DiagnosticId);

    [Fact]
    public Task SCA1004_PrivateField() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA1004-PrivateField",
            new NamingConventionAnalyzer(),
            @"C:\Repo\Assets\Scripts\Core\Sample.cs",
            NamingConventionAnalyzer.DiagnosticIdPrefix);

    [Fact]
    public Task SCA1005_PascalCase() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA1005-PascalCase",
            new NamingConventionAnalyzer(),
            @"C:\Repo\Assets\Scripts\Infra\Processor.cs",
            NamingConventionAnalyzer.DiagnosticIdPascal);

    [Fact]
    public Task SCA6001_SerializableBacking() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA6001-SerializableBacking",
            new SerializableBackingFieldAnalyzer(),
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Runtime\ViewModel.cs");

    [Fact]
    public Task SCA2004_StaticMethodScope() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA2004-StaticMethodScope",
            new StaticMethodScopeAnalyzer(),
            @"C:\Repo\Assets\Scripts\GameEngine\Runtime\Game.cs");

    [Fact]
    public Task SCA3002_TypePlacement() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA3002-TypePlacement",
            new TypePlacementAnalyzer(),
            @"C:\Repo\Assets\Scripts\Core\GameEngine\Runtime\Game.cs");

    [Fact]
    public Task SCA6002_TextMeshPro() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA6002-TextMeshPro",
            new TextMeshProUsageAnalyzer(),
            @"C:\Repo\Assets\Scripts\App\MainMenu\Runtime\MainMenuView.cs");

    [Fact]
    public Task SCA3003_ClassMemberOrder() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA3003-ClassMemberOrder",
            new ClassMemberOrderAnalyzer(),
            @"C:\Repo\Assets\Scripts\Core\Sample.cs");

    [Fact]
    public Task SCA3004_MultipleNamespaces() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA3004-MultipleNamespaces",
            new SingleTopLevelNamespaceAnalyzer(),
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Contracts\INavigation.cs",
            SingleTopLevelNamespaceAnalyzer.DiagnosticId);

    [Fact]
    public Task SCA3004_TypeOutsideNamespace() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA3004-TypeOutsideNamespace",
            new SingleTopLevelNamespaceAnalyzer(),
            @"C:\Repo\Assets\Scripts\Infra\Navigation\Contracts\INavigation.cs",
            SingleTopLevelNamespaceAnalyzer.DiagnosticId);

    [Fact]
    public Task SCA1006_LoopBrace() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA1006-LoopBrace",
            new LoopBraceAnalyzer(),
            @"C:\Repo\Assets\Scripts\Core\Sample.cs");

    [Fact]
    public Task SCA1007_ConditionalBrace() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA1007-ConditionalBrace",
            new ConditionalBraceAnalyzer(),
            @"C:\Repo\Assets\Scripts\Core\Sample.cs");

    [Fact]
    public Task SCA8001_DeadCode() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA8001-DeadCode",
            new DeadCodeInRuntimeAnalyzer(),
            @"C:\Repo\Assets\Scripts\Core\Feature\Runtime\Sample.cs");

    [Fact]
    public Task SCA8002_Pragma() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA8002-Pragma",
            new PragmaWarningDisableAnalyzer(),
            @"C:\Repo\Assets\Scripts\Core\Feature\Runtime\Sample.cs");

    [Fact]
    public Task SCA2005_NestedStaticType() =>
        DiagnosticGoldenFixture.AssertMatchesGoldenAsync(
            "SCA2005-NestedStaticType",
            new NestedStaticTypeInInstanceAnalyzer(),
            @"C:\Repo\Assets\Scripts\Core\Sample.cs");
}
