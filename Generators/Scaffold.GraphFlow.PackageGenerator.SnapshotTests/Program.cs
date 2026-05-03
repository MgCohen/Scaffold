// Snapshot harness for Scaffold.GraphFlow.PackageGenerator.
// Runs the deployed generator against two fixtures (runtime asm + editor asm shape) and diffs
// the emitted trees against checked-in .expected files. Pass --update to refresh snapshots.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

var update = args.Any(a => a == "--update");
var here = Path.GetDirectoryName(Path.GetFullPath(Environment.ProcessPath ?? "")) ?? Directory.GetCurrentDirectory();
// climb out of bin/Debug/netX.0/ to project root
while (here != null && !File.Exists(Path.Combine(here, "Scaffold.GraphFlow.PackageGenerator.SnapshotTests.csproj")))
{
    here = Path.GetDirectoryName(here);
}

if (here == null)
{
    Console.Error.WriteLine("Could not locate snapshot project root.");
    return 2;
}

var snapshotsRoot = Path.Combine(here, "Snapshots");
// Repo root = parent of Generators/.
var repoRoot = Path.GetFullPath(Path.Combine(here, "..", ".."));

// Resolve DLLs relative to the repo. Allow SCAFFOLD_GRAPHFLOW_DLLS env override (semicolon-delimited
// "generator;attributes;runtime") for CI / out-of-tree harness use.
string GeneratorDll, AttrDll, RuntimeDll;
var envOverride = Environment.GetEnvironmentVariable("SCAFFOLD_GRAPHFLOW_DLLS");
if (!string.IsNullOrEmpty(envOverride))
{
    var parts = envOverride.Split(';');
    if (parts.Length != 3)
    {
        Console.Error.WriteLine("SCAFFOLD_GRAPHFLOW_DLLS must be three ';'-separated paths: generator;attributes;runtime");
        return 2;
    }
    GeneratorDll = parts[0];
    AttrDll = parts[1];
    RuntimeDll = parts[2];
}
else
{
    GeneratorDll = Path.Combine(repoRoot, "Assets", "Packages", "com.scaffold.graphflow", "Generators", "Scaffold.GraphFlow.PackageGenerator.dll");
    AttrDll = Path.Combine(repoRoot, "Assets", "Packages", "com.scaffold.graphflow", "Runtime", "Attributes", "Scaffold.GraphFlow.AttributesLib.dll");
    // M0 sandbox runtime asm; phase 5 demotes it to Samples~/. Keep the Library path until then.
    RuntimeDll = Path.Combine(repoRoot, "Library", "ScriptAssemblies", "Scaffold.GraphFlow.M0.dll");
}

// Add the package's runtime DLL to the references so the new D5 markers (IGraphEntry<,>, Unit, etc.)
// resolve. The package runtime asm is named Scaffold.GraphFlow.dll.
var PackageRuntimeDll = Path.Combine(repoRoot, "Library", "ScriptAssemblies", "Scaffold.GraphFlow.dll");

const string PackageAttr = @"
using Scaffold.GraphFlow;
using Scaffold.GraphFlow.M0.Smoke;
[assembly: GraphPackage(
    Runner = typeof(MySmokeRunner),
    Extension = ""gfmsmoke"",
    AssetMenu = ""GraphFlow/M0 Smoke Graph"",
    Convention = PortConvention.AllFieldsIn,
    RegistryNamespace = ""Scaffold.GraphFlow.M0.Generated"",
    DispatcherBase = typeof(MyDispatcherBase<,>))]
";

var failures = new List<string>();
RunFixture("Runtime", "Scaffold.GraphFlow.M0", snapshotsRoot, update, failures);
RunFixture("Editor", "Scaffold.GraphFlow.M0.Editor", snapshotsRoot, update, failures);

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"FAIL: {failures.Count} snapshot mismatch(es). Re-run with --update to refresh.");
    foreach (var f in failures) Console.Error.WriteLine("  " + f);
    return 1;
}

Console.WriteLine("OK: all snapshots match.");
return 0;

void RunFixture(string fixtureName, string asmName, string snapshotsRoot, bool update, List<string> failures)
{
    var refs = new List<MetadataReference>
    {
        MetadataReference.CreateFromFile(AttrDll),
        MetadataReference.CreateFromFile(RuntimeDll),
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
    };
    if (File.Exists(PackageRuntimeDll))
    {
        refs.Add(MetadataReference.CreateFromFile(PackageRuntimeDll));
    }
    var corelibDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
    foreach (var n in new[] { "System.Runtime.dll", "netstandard.dll", "System.Collections.dll" })
    {
        var p = Path.Combine(corelibDir, n);
        if (File.Exists(p)) refs.Add(MetadataReference.CreateFromFile(p));
    }

    var compilation = CSharpCompilation.Create(
        asmName,
        new[] { CSharpSyntaxTree.ParseText(PackageAttr) },
        refs,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    var asm = Assembly.LoadFrom(GeneratorDll);
    var generators = asm.GetTypes()
        .Where(t => t.GetCustomAttributes(false).Any(a => a.GetType().Name == "GeneratorAttribute"))
        .Select(t => (IIncrementalGenerator)Activator.CreateInstance(t)!)
        .Select(ig => ig.AsSourceGenerator())
        .ToImmutableArray();

    GeneratorDriver driver = CSharpGeneratorDriver.Create(generators);
    driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
    var run = driver.GetRunResult();

    var fixtureDir = Path.Combine(snapshotsRoot, fixtureName);
    Directory.CreateDirectory(fixtureDir);
    var emitted = run.GeneratedTrees.ToDictionary(t => Path.GetFileName(t.FilePath), t => Normalize(t.GetText().ToString()));

    foreach (var (name, content) in emitted)
    {
        var expectedPath = Path.Combine(fixtureDir, name + ".expected");
        if (!File.Exists(expectedPath))
        {
            if (update)
            {
                File.WriteAllText(expectedPath, content);
                Console.WriteLine($"[NEW] {fixtureName}/{name}");
                continue;
            }

            failures.Add($"{fixtureName}/{name}: no snapshot on disk (run --update to create)");
            continue;
        }

        var expected = Normalize(File.ReadAllText(expectedPath));
        if (expected == content)
        {
            Console.WriteLine($"[OK ] {fixtureName}/{name}");
            continue;
        }

        if (update)
        {
            File.WriteAllText(expectedPath, content);
            Console.WriteLine($"[UPD] {fixtureName}/{name}");
        }
        else
        {
            failures.Add($"{fixtureName}/{name}: content differs");
            Console.Error.WriteLine($"[MIS] {fixtureName}/{name}");
        }
    }

    foreach (var orphan in Directory.GetFiles(fixtureDir, "*.expected"))
    {
        var name = Path.GetFileNameWithoutExtension(orphan);
        if (!emitted.ContainsKey(name))
        {
            if (update)
            {
                File.Delete(orphan);
                Console.WriteLine($"[DEL] {fixtureName}/{name}");
            }
            else
            {
                failures.Add($"{fixtureName}/{name}: snapshot has no corresponding generator output");
            }
        }
    }
}

static string Normalize(string s) => s.Replace("\r\n", "\n");
