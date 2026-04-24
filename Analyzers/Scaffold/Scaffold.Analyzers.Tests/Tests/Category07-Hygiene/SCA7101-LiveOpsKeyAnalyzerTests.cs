using System.Threading.Tasks;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class SCA7101LiveOpsKeyAnalyzerTests
{
    [Fact]
    public async Task SCA7101_WhenIGameModuleDataWithoutAttribute_Reports()
    {
        const string source = @"
using System;
namespace LiveOps.DTO.Keys
{
    [AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public sealed class LiveOpsKeyAttribute : System.Attribute
    {
        public string Value { get; }
        public LiveOpsKeyAttribute(string value) => Value = value;
    }
}
namespace LiveOps.DTO.GameModule
{
    public interface IGameModuleData { }
}
namespace LiveOps.DTO.ModuleRequest
{
    public abstract class ModuleRequest { }
}
namespace LiveOps.Modules.DTO.Sample
{
    public sealed class NoAttr : global::LiveOps.DTO.GameModule.IGameModuleData
    {
    }

    [global::LiveOps.DTO.Keys.LiveOpsKey(""Ok"")]
    public sealed class WithAttr : global::LiveOps.DTO.GameModule.IGameModuleData
    {
    }
}
";

        var d = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\LiveOps\Modules\LiveOps.Modules.DTO\X.cs",
            new LiveOpsKeyAnalyzers(),
            LiveOpsKeyAnalyzers.SCA7101);

        Assert.Single(d);
        Assert.Contains("NoAttr", d[0].GetMessage());
    }

    [Fact]
    public async Task SCA7101_WhenModuleRequestWithoutAttribute_Reports()
    {
        const string source = @"
using System;
namespace LiveOps.DTO.Keys
{
    [AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public sealed class LiveOpsKeyAttribute : System.Attribute
    {
        public string Value { get; }
        public LiveOpsKeyAttribute(string value) => Value = value;
    }
}
namespace LiveOps.DTO.ModuleRequest
{
    public abstract class ModuleRequest { }
    public abstract class ModuleRequest<T> : ModuleRequest where T : class { }
    public class ModuleResponse { }
}
namespace LiveOps.Modules.DTO.Sample
{
    public sealed class BadReq : global::LiveOps.DTO.ModuleRequest.ModuleRequest<global::LiveOps.DTO.ModuleRequest.ModuleResponse>
    {
    }
}
";

        var d = await AnalyzerTestHarness.GetDiagnosticsByIdAsync(
            source,
            @"C:\Repo\LiveOps\Modules\LiveOps.Modules.DTO\Y.cs",
            new LiveOpsKeyAnalyzers(),
            LiveOpsKeyAnalyzers.SCA7101);

        Assert.Single(d);
        Assert.Contains("BadReq", d[0].GetMessage());
    }
}
