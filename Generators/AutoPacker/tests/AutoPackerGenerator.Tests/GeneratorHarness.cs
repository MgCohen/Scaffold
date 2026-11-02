using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using Scaffold.AutoPacker;

namespace AutoPackerGenerator.Tests
{
    // Runs the generator on source text and compiles the result.
    internal static class GeneratorHarness
    {
        private static readonly MetadataReference[] BaseReferences = LoadBaseReferences();

        private static MetadataReference[] LoadBaseReferences()
        {
            var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
            var references = paths.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToList();
            references.Add(MetadataReference.CreateFromFile(typeof(AutoPackAttribute).Assembly.Location));
            return references.ToArray();
        }

        // Runs the generator and returns the compilation with the generated source.
        // Fails the test if the generator or the compiler reports an error.
        public static Compilation Generate(string assemblyName, string source, params MetadataReference[] references)
        {
            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)) },
                BaseReferences.Concat(references),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

            GeneratorDriver driver = CSharpGeneratorDriver.Create(new global::AutoPackerGenerator.AutoPackerGenerator());
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

            AssertNoErrors(assemblyName + " generator", generatorDiagnostics);
            AssertNoErrors(assemblyName + " compiler", output.GetDiagnostics());
            return output;
        }

        public static byte[] Emit(Compilation compilation)
        {
            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            AssertNoErrors(compilation.AssemblyName + " emit", result.Diagnostics);
            return stream.ToArray();
        }

        // Loads the images in a new load context. Contracts resolve from the default context,
        // so IPackedStruct and the other contract types are the same types as in the test.
        public static Assembly[] Load(params byte[][] images)
        {
            var context = new AssemblyLoadContext("AutoPackerTest" + Guid.NewGuid(), isCollectible: true);
            return images.Select(image => context.LoadFromStream(new MemoryStream(image))).ToArray();
        }

        private static void AssertNoErrors(string stage, IEnumerable<Diagnostic> diagnostics)
        {
            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
            if (errors.Length > 0)
                Assert.Fail($"{stage} reported errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));
        }
    }
}
