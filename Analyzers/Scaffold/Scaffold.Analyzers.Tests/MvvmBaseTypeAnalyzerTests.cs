using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Scaffold.Analyzers.Tests
{
    public sealed class MvvmBaseTypeAnalyzerTests
    {
        [Fact]
        public async Task Diagnostic_WhenClassImplementsIViewModelWithoutViewModelBase()
        {
            const string source = @"
namespace Scaffold.MVVM
{
    public interface IViewModel {}

    public class InventoryViewModel : IViewModel
    {
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source, @"C:\Repo\Assets\Scripts\Core\MVVM\Runtime\Implementation\InventoryViewModel.cs");
            Assert.Single(diagnostics);
            Assert.Equal(MvvmBaseTypeAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task NoDiagnostic_WhenClassInheritsViewModel()
        {
            const string source = @"
namespace Scaffold.MVVM
{
    public interface IViewModel {}

    public abstract class ViewModel : IViewModel
    {
    }

    public partial class InventoryViewModel : ViewModel
    {
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source, @"C:\Repo\Assets\Scripts\Core\MVVM\Runtime\Implementation\InventoryViewModel.cs");
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task Diagnostic_WhenClassImplementsInpcWithoutModelOrViewModelBase()
        {
            const string source = @"
using System.ComponentModel;

namespace Scaffold.MVVM
{
    public class InventoryState : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source, @"C:\Repo\Assets\Scripts\Core\MVVM\Runtime\Implementation\InventoryState.cs");
            Assert.Single(diagnostics);
            Assert.Equal(MvvmBaseTypeAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source, string filePath)
        {
            var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp12);
            var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions, filePath);
            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.GCSettings).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.ComponentModel.INotifyPropertyChanged).Assembly.Location),
            };

            var compilation = CSharpCompilation.Create(
                "AnalyzerTests",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var analyzer = new MvvmBaseTypeAnalyzer();
            var optionsProvider = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>());
            var analyzerOptionsInstance = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, optionsProvider);
            var compilationWithAnalyzers = compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
                new CompilationWithAnalyzersOptions(analyzerOptionsInstance, onAnalyzerException: null, concurrentAnalysis: false, logAnalyzerExecutionTime: false));

            var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
            return diagnostics.Where(diagnostic => diagnostic.Id == MvvmBaseTypeAnalyzer.DiagnosticId).ToImmutableArray();
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
                if (values.TryGetValue(key, out value))
                {
                    return true;
                }

                value = string.Empty;
                return false;
            }
        }
    }
}
