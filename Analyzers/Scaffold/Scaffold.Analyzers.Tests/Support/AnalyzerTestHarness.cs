using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers.Tests;

/// <summary>
/// Single-tree and structural-graph test helpers. Use <see cref="LoadTestDataText"/> for file-based snippets under <c>TestData/</c>.
/// </summary>
internal static class AnalyzerTestHarness
{
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        StructuralTestGraph graph,
        DiagnosticAnalyzer analyzer,
        IDictionary<string, string>? analyzerOptions = null,
        bool includeUnityEngineReference = false)
    {
        var model = graph?.Model ?? throw new ArgumentNullException(nameof(graph));
        var rootNode = model.Assemblies.FirstOrDefault(node => string.Equals(node.Name, model.RootAssemblyName, StringComparison.OrdinalIgnoreCase));
        if (rootNode == null)
        {
            throw new InvalidOperationException($"Root assembly '{model.RootAssemblyName}' is not present in structural graph.");
        }

        using var workspace = StructuralGraphWorkspace.Create(model);
        return await GetDiagnosticsAsync(
            string.Join(Environment.NewLine + Environment.NewLine, rootNode.SourceFiles.Select(source => source.Content)),
            workspace.GetPrimarySourcePath(model.RootAssemblyName),
            analyzer,
            analyzerOptions,
            includeUnityEngineReference,
            model.RootAssemblyName,
            workspace.GetMetadataReferenceAssemblyNames(model.RootAssemblyName),
            workspace.GetMetadataReferenceSources());
    }

    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsByIdAsync(
        StructuralTestGraph graph,
        DiagnosticAnalyzer analyzer,
        string diagnosticId,
        IDictionary<string, string>? analyzerOptions = null,
        bool includeUnityEngineReference = false)
    {
        var diagnostics = await GetDiagnosticsAsync(graph, analyzer, analyzerOptions, includeUnityEngineReference);
        return diagnostics.Where(diagnostic => diagnostic.Id == diagnosticId).ToImmutableArray();
    }

    public static Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        string source,
        string filePath,
        DiagnosticAnalyzer analyzer,
        IDictionary<string, string>? analyzerOptions = null,
        bool includeUnityEngineReference = false,
        string compilationAssemblyName = "AnalyzerTests",
        IEnumerable<string>? additionalAssemblyNames = null,
        IDictionary<string, string>? assemblySources = null) =>
        GetDiagnosticsAsync(
            source,
            filePath,
            ImmutableArray.Create(analyzer),
            analyzerOptions,
            includeUnityEngineReference,
            compilationAssemblyName,
            additionalAssemblyNames,
            assemblySources);

    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        string source,
        string filePath,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        IDictionary<string, string>? analyzerOptions = null,
        bool includeUnityEngineReference = false,
        string compilationAssemblyName = "AnalyzerTests",
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
            analyzers,
            new CompilationWithAnalyzersOptions(
                analyzerOptionsInstance,
                onAnalyzerException: null,
                concurrentAnalysis: false,
                logAnalyzerExecutionTime: false));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    /// <summary>
    /// Reads UTF-8 text from <c>TestData/{relativePathUnderTestData}</c> next to the test assembly (see test csproj CopyToOutputDirectory).
    /// </summary>
    public static string LoadTestDataText(string relativePathUnderTestData)
    {
        var baseDir = Path.GetDirectoryName(typeof(AnalyzerTestHarness).Assembly.Location) ?? string.Empty;
        var path = Path.Combine(baseDir, "TestData", relativePathUnderTestData.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Test data not found: {path}", path);
        }

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Runs an analyzer on file-based source from <see cref="LoadTestDataText"/> with optional <c>dotnet_diagnostic.*</c> / scaffold option pairs.
    /// </summary>
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsFromTestDataAsync(
        string relativePathUnderTestData,
        string syntheticFilePath,
        DiagnosticAnalyzer analyzer,
        IDictionary<string, string>? analyzerOptions = null,
        bool includeUnityEngineReference = false,
        string compilationAssemblyName = "AnalyzerTests",
        IEnumerable<string>? additionalAssemblyNames = null)
    {
        var source = LoadTestDataText(relativePathUnderTestData);
        return await GetDiagnosticsAsync(
            source,
            syntheticFilePath,
            analyzer,
            analyzerOptions,
            includeUnityEngineReference,
            compilationAssemblyName,
            additionalAssemblyNames,
            assemblySources: null);
    }

    /// <summary>
    /// Builds a single-key options map for <c>dotnet_diagnostic.{id}.severity</c> (template for matrix tests).
    /// </summary>
    public static Dictionary<string, string> CreateDotnetDiagnosticSeverityOptions(string diagnosticId, string severity)
    {
        return new Dictionary<string, string>
        {
            ["dotnet_diagnostic." + diagnosticId + ".severity"] = severity
        };
    }

    public static Task<ImmutableArray<Diagnostic>> GetDiagnosticsByIdAsync(
        string source,
        string filePath,
        DiagnosticAnalyzer analyzer,
        string diagnosticId,
        IDictionary<string, string>? analyzerOptions = null,
        bool includeUnityEngineReference = false,
        string compilationAssemblyName = "AnalyzerTests",
        IEnumerable<string>? additionalAssemblyNames = null) =>
        GetDiagnosticsByIdAsync(
            source,
            filePath,
            ImmutableArray.Create(analyzer),
            diagnosticId,
            analyzerOptions,
            includeUnityEngineReference,
            compilationAssemblyName,
            additionalAssemblyNames);

    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsByIdAsync(
        string source,
        string filePath,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        string diagnosticId,
        IDictionary<string, string>? analyzerOptions = null,
        bool includeUnityEngineReference = false,
        string compilationAssemblyName = "AnalyzerTests",
        IEnumerable<string>? additionalAssemblyNames = null)
    {
        var diagnostics = await GetDiagnosticsAsync(
            source,
            filePath,
            analyzers,
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

    private sealed class StructuralGraphWorkspace : IDisposable
    {
        private readonly string workspaceRoot;
        private readonly Dictionary<string, string> primarySourcePathByAssembly;
        private readonly Dictionary<string, string> synthesizedSourcesByAssembly;
        private readonly Dictionary<string, ImmutableArray<string>> directReferencesByAssembly;

        private StructuralGraphWorkspace(
            string workspaceRoot,
            Dictionary<string, string> primarySourcePathByAssembly,
            Dictionary<string, string> synthesizedSourcesByAssembly,
            Dictionary<string, ImmutableArray<string>> directReferencesByAssembly)
        {
            this.workspaceRoot = workspaceRoot;
            this.primarySourcePathByAssembly = primarySourcePathByAssembly;
            this.synthesizedSourcesByAssembly = synthesizedSourcesByAssembly;
            this.directReferencesByAssembly = directReferencesByAssembly;
        }

        public static StructuralGraphWorkspace Create(StructuralTestGraphModel model)
        {
            var workspaceRoot = Path.Combine(Path.GetTempPath(), "analyzer-structural-graph-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspaceRoot);

            var sourcePathByAssembly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var sourceByAssembly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var directReferencesByAssembly = new Dictionary<string, ImmutableArray<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var assembly in model.Assemblies)
            {
                string? firstPath = null;
                var sourceBuilder = new StringBuilder();
                for (var i = 0; i < assembly.SourceFiles.Length; i++)
                {
                    var sourceFile = assembly.SourceFiles[i];
                    var absolutePath = Path.Combine(workspaceRoot, sourceFile.Path.Replace('/', Path.DirectorySeparatorChar));
                    var directoryPath = Path.GetDirectoryName(absolutePath);
                    if (!string.IsNullOrWhiteSpace(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    File.WriteAllText(absolutePath, sourceFile.Content);
                    firstPath ??= absolutePath;

                    if (i > 0)
                    {
                        sourceBuilder.AppendLine();
                    }

                    sourceBuilder.AppendLine(sourceFile.Content);
                }

                if (firstPath == null)
                {
                    throw new InvalidOperationException($"Assembly '{assembly.Name}' has no sources.");
                }

                sourcePathByAssembly[assembly.Name] = firstPath;
                sourceByAssembly[assembly.Name] = sourceBuilder.ToString();
                directReferencesByAssembly[assembly.Name] = assembly.References;

                var asmdefPath = TryGetAsmdefPath(workspaceRoot, assembly);
                if (!string.IsNullOrWhiteSpace(asmdefPath))
                {
                    var asmdefDirectory = Path.GetDirectoryName(asmdefPath);
                    if (!string.IsNullOrWhiteSpace(asmdefDirectory))
                    {
                        Directory.CreateDirectory(asmdefDirectory);
                    }

                    var referencesJson = assembly.References.Length == 0
                        ? string.Empty
                        : string.Join(", ", assembly.References.Select(reference => "\"" + reference + "\""));
                    var asmdefContent = "{ \"name\": \"" + assembly.Name + "\", \"references\": [" + referencesJson + "] }";
                    File.WriteAllText(asmdefPath, asmdefContent);
                }
            }

            return new StructuralGraphWorkspace(workspaceRoot, sourcePathByAssembly, sourceByAssembly, directReferencesByAssembly);
        }

        public string GetPrimarySourcePath(string assemblyName) => primarySourcePathByAssembly[assemblyName];

        public IEnumerable<string> GetMetadataReferenceAssemblyNames(string assemblyName)
        {
            if (!directReferencesByAssembly.TryGetValue(assemblyName, out var references))
            {
                return Enumerable.Empty<string>();
            }

            return references;
        }

        public IDictionary<string, string> GetMetadataReferenceSources()
        {
            return synthesizedSourcesByAssembly;
        }

        public void Dispose()
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }

        private static string TryGetAsmdefPath(string workspaceRoot, StructuralAssemblyNode assembly)
        {
            var candidate = assembly.SourceFiles.FirstOrDefault(source =>
                source.Path.IndexOf("/Assets/Scripts/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                source.Path.IndexOf("Assets/Scripts/", StringComparison.OrdinalIgnoreCase) == 0);
            if (candidate == null)
            {
                return string.Empty;
            }

            var relativePath = candidate.Path.Replace('\\', '/');
            var directory = Path.GetDirectoryName(relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(directory))
            {
                return string.Empty;
            }

            return Path.Combine(workspaceRoot, directory, assembly.Name + ".asmdef");
        }
    }
}
