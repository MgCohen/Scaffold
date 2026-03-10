using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Scaffold.Analyzers.Tests;

public sealed class InvariantEntryPointAnalyzerTests
{
    [Fact]
    public async Task NoDiagnostic_WhenLeadingValidateCall()
    {
        const string source = @"
namespace Scaffold.Infra.NetworkMessages
{
    public class Dispatcher
    {
        public void Send(string message)
        {
            ValidateMessage(message);
            Publish(message);
        }

        private void ValidateMessage(string message) { }
        private void Publish(string message) { }
    }
}";

        var diagnostics = await GetDiagnosticsAsync(source, @"C:\Repo\Assets\Scripts\Infra\NetworkMessages\Runtime\Implementation\Dispatcher.cs");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenLeadingGuardClause()
    {
        const string source = @"
namespace Scaffold.Infra.NetworkMessages
{
    public class Dispatcher
    {
        public void Send(string message)
        {
            if (message == null) throw new System.ArgumentNullException(nameof(message));
            Publish(message);
        }

        private void Publish(string message) { }
    }
}";

        var diagnostics = await GetDiagnosticsAsync(source, @"C:\Repo\Assets\Scripts\Infra\NetworkMessages\Runtime\Implementation\Dispatcher.cs");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Diagnostic_WhenNoEntryValidation()
    {
        const string source = @"
namespace Scaffold.Infra.NetworkMessages
{
    public class Dispatcher
    {
        public void Send(string message)
        {
            Publish(message);
        }

        private void Publish(string message) { }
    }
}";

        var diagnostics = await GetDiagnosticsAsync(source, @"C:\Repo\Assets\Scripts\Infra\NetworkMessages\Runtime\Implementation\Dispatcher.cs");
        Assert.Single(diagnostics);
        Assert.Equal(InvariantEntryPointAnalyzer.DiagnosticId, diagnostics[0].Id);
    }

    [Fact]
    public async Task NoDiagnostic_ForNonPublicMethod()
    {
        const string source = @"
namespace Scaffold.Infra.NetworkMessages
{
    public class Dispatcher
    {
        internal void Send(string message)
        {
            Publish(message);
        }

        private void Publish(string message) { }
    }
}";

        var diagnostics = await GetDiagnosticsAsync(source, @"C:\Repo\Assets\Scripts\Infra\NetworkMessages\Runtime\Implementation\Dispatcher.cs");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_ForTestsAndSamplesPaths()
    {
        const string source = @"
namespace Scaffold.Infra.NetworkMessages
{
    public class Dispatcher
    {
        public void Send(string message)
        {
            Publish(message);
        }

        private void Publish(string message) { }
    }
}";

        var testsDiagnostics = await GetDiagnosticsAsync(source, @"C:\Repo\Assets\Scripts\Infra\NetworkMessages\Tests\DispatcherTests.cs");
        var samplesDiagnostics = await GetDiagnosticsAsync(source, @"C:\Repo\Assets\Scripts\Infra\NetworkMessages\Samples\DispatcherSample.cs");

        Assert.Empty(testsDiagnostics);
        Assert.Empty(samplesDiagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_ForOverrideAndInterfaceMethods()
    {
        const string source = @"
namespace Scaffold.Infra.NetworkMessages
{
    public interface IDispatcher
    {
        void Send(string message);
    }

    public abstract class BaseDispatcher
    {
        public abstract void Send(string message);
    }

    public class Dispatcher : BaseDispatcher
    {
        public override void Send(string message)
        {
            Publish(message);
        }

        private void Publish(string message) { }
    }
}";

        var diagnostics = await GetDiagnosticsAsync(source, @"C:\Repo\Assets\Scripts\Infra\NetworkMessages\Runtime\Implementation\Dispatcher.cs");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoDiagnostic_WhenConfiguredPrefixIsUsed()
    {
        const string source = @"
namespace Scaffold.Infra.NetworkMessages
{
    public class Dispatcher
    {
        public void Send(string message)
        {
            CheckInvariant(message);
            Publish(message);
        }

        private void CheckInvariant(string message) { }
        private void Publish(string message) { }
    }
}";

        var options = new Dictionary<string, string>
        {
            ["scaffold.SCA0014.allowed_prefixes"] = "Check,Assert"
        };

        var diagnostics = await GetDiagnosticsAsync(
            source,
            @"C:\Repo\Assets\Scripts\Infra\NetworkMessages\Runtime\Implementation\Dispatcher.cs",
            options);

        Assert.Empty(diagnostics);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        string source,
        string filePath,
        IDictionary<string, string>? analyzerOptions = null)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp12);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions, filePath);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.GCSettings).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "AnalyzerTests",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new InvariantEntryPointAnalyzer();
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(analyzerOptions ?? new Dictionary<string, string>());
        var analyzerOptionsInstance = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, optionsProvider);
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new CompilationWithAnalyzersOptions(analyzerOptionsInstance, onAnalyzerException: null, concurrentAnalysis: false, logAnalyzerExecutionTime: false));

        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        return diagnostics.Where(diagnostic => diagnostic.Id == InvariantEntryPointAnalyzer.DiagnosticId).ToImmutableArray();
    }

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions globalOptions;

        public TestAnalyzerConfigOptionsProvider(IDictionary<string, string> globalOptionsMap)
        {
            globalOptions = new TestAnalyzerConfigOptions(globalOptionsMap);
        }

        public override AnalyzerConfigOptions GlobalOptions => globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return globalOptions;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return globalOptions;
        }
    }

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly IDictionary<string, string> values;

        public TestAnalyzerConfigOptions(IDictionary<string, string> values)
        {
            this.values = values;
        }

        public override bool TryGetValue(string key, out string value)
        {
            return values.TryGetValue(key, out value!);
        }
    }
}
