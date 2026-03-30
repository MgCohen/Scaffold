using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Mvvm.Analyzers.Tests;

// Single-tree compilation harness for MVVM analyzers only (no dependency on Scaffold.Analyzers.Tests).
internal static class MvvmAnalyzerTestHarness
{
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        string source,
        string filePath,
        DiagnosticAnalyzer analyzer,
        IDictionary<string, string>? analyzerOptions = null,
        bool includeUnityEngineReference = false,
        string compilationAssemblyName = "MvvmAnalyzerTests",
        IEnumerable<string>? additionalAssemblyNames = null,
        IDictionary<string, string>? assemblySources = null)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp12);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions, filePath);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.GCSettings).Assembly.Location),
        };

        if (includeUnityEngineReference)
        {
            references.Add(CreateUnityEngineCoreModuleReference());
        }

        if (additionalAssemblyNames != null)
        {
            foreach (var assemblyName in additionalAssemblyNames)
            {
                if (string.IsNullOrWhiteSpace(assemblyName)) continue;
                if (assemblySources != null && assemblySources.TryGetValue(assemblyName, out var assemblySource))
                {
                    references.Add(CreateReferenceAssembly(assemblyName, assemblySource));
                    continue;
                }

                references.Add(CreateReferenceAssembly(assemblyName));
            }
        }

        var compilation = CSharpCompilation.Create(
            compilationAssemblyName,
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var optionsProvider = new TestAnalyzerConfigOptionsProvider(analyzerOptions ?? new Dictionary<string, string>());
        var analyzerOptionsInstance = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, optionsProvider);
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create(analyzer),
            new CompilationWithAnalyzersOptions(
                analyzerOptionsInstance,
                onAnalyzerException: null,
                concurrentAnalysis: false,
                logAnalyzerExecutionTime: false));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsByIdAsync(
        string source,
        string filePath,
        DiagnosticAnalyzer analyzer,
        string diagnosticId,
        IDictionary<string, string>? analyzerOptions = null,
        bool includeUnityEngineReference = false,
        string compilationAssemblyName = "MvvmAnalyzerTests",
        IEnumerable<string>? additionalAssemblyNames = null)
    {
        var diagnostics = await GetDiagnosticsAsync(
            source,
            filePath,
            analyzer,
            analyzerOptions,
            includeUnityEngineReference,
            compilationAssemblyName,
            additionalAssemblyNames,
            assemblySources: null);
        return diagnostics.Where(diagnostic => diagnostic.Id == diagnosticId).ToImmutableArray();
    }

    private static MetadataReference CreateUnityEngineCoreModuleReference()
    {
        const string unityStub = @"
namespace UnityEngine
{
    public class Object { }
    public class MonoBehaviour : Object { }
    public class ScriptableObject : Object { }
    public sealed class SerializeField : System.Attribute { }
}";
        return CreateReferenceAssembly("UnityEngine.CoreModule", unityStub);
    }

    private static MetadataReference CreateReferenceAssembly(string assemblyName, string source = "namespace Stub { public sealed class Placeholder { } }")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp12));
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location)
        };

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        if (!emitResult.Success)
        {
            throw new InvalidOperationException($"Failed to emit reference assembly stub '{assemblyName}'.");
        }

        stream.Position = 0;
        return MetadataReference.CreateFromImage(stream.ToArray());
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
