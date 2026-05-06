// Snapshot harness for Scaffold.GraphFlow.PackageGenerator.
// Runs the deployed generator against two fixtures (runtime asm + editor asm shape) compiled
// from a self-contained synthetic source — no dependency on any Unity sample's built DLL.
// The synthetic source declares a minimal runner + payloads that exercise every emission path
// (Mode-1 entry/action, Mode-2 command/dispatcher, [GraphEvent], [GraphNode] data node);
// the generator's catalog emission needs the runner in the SAME compilation as the
// [assembly:GraphPackage] decl — including the synthetic source as a syntax tree achieves that.
//
// Pass --update to refresh snapshots.

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
// "generator;attributes;packageRuntime") for CI / out-of-tree harness use.
string GeneratorDll, AttrDll, PackageRuntimeDll;
var envOverride = Environment.GetEnvironmentVariable("SCAFFOLD_GRAPHFLOW_DLLS");
if (!string.IsNullOrEmpty(envOverride))
{
    var parts = envOverride.Split(';');
    if (parts.Length != 3)
    {
        Console.Error.WriteLine("SCAFFOLD_GRAPHFLOW_DLLS must be three ';'-separated paths: generator;attributes;packageRuntime");
        return 2;
    }
    GeneratorDll       = parts[0];
    AttrDll            = parts[1];
    PackageRuntimeDll  = parts[2];
}
else
{
    GeneratorDll       = Path.Combine(repoRoot, "Assets", "Packages", "com.scaffold.graphflow", "Generators", "Scaffold.GraphFlow.PackageGenerator.dll");
    AttrDll            = Path.Combine(repoRoot, "Assets", "Packages", "com.scaffold.graphflow", "Runtime", "Attributes", "Scaffold.GraphFlow.AttributesLib.dll");
    PackageRuntimeDll  = Path.Combine(repoRoot, "Library", "ScriptAssemblies", "Scaffold.GraphFlow.dll");
}

// Synthetic fixture source. Compiled into both fixtures so the runner type lives in the same
// compilation as the [assembly:GraphPackage] attribute (lets EmitCatalogIfRunnerAsm fire on the
// runtime pass). Carries one of each emission shape: Mode-1 entry, Mode-1 IExecutable action,
// Mode-2 command + dispatcher base, [GraphEvent], [GraphNode] data node.
const string SyntheticSource = @"
using System.Threading.Tasks;
using Scaffold.GraphFlow;

namespace HarnessFixture
{
    public sealed class HarnessRunner : GraphRunner
    {
        public string LastLog = """";
    }

    public abstract class HarnessCommand<TResult> { }

    public abstract class HarnessDispatcherBase<TCmd, TResult> : RuntimeNode<HarnessRunner>
        where TCmd : HarnessCommand<TResult>, new()
    {
        public FlowInPort FlowIn = null!;
        public FlowOutPort FlowOut = null!;
        protected HarnessDispatcherBase()
        {
            FlowIn = new FlowInPort(this);
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Ports.Add(FlowIn.Name, FlowIn);
            Ports.Add(FlowOut.Name, FlowOut);
        }
        public sealed override Task Execute(HarnessRunner runner, Flow flow) => flow.GoTo(FlowOut);
    }

    public sealed class OnEntry : IGraphEntry
    {
        [GraphPort] public int Value;
    }

    [GraphEvent]
    public sealed class HarnessEvent
    {
        public int Amount;
        public string Label = """";
    }

    public sealed class EchoCmd : HarnessCommand<EchoResult>, IGraphAction<HarnessRunner>
    {
        [GraphPort] public int Magnitude;
    }

    public sealed class EchoResult
    {
        [GraphPort] public string Summary = """";
    }

    public sealed class LogAction : IGraphAction<HarnessRunner>, IExecutable<HarnessRunner>
    {
        [GraphPort] public string Message = """";
        public Task Execute(HarnessRunner runner) { runner.LastLog = Message; return Task.CompletedTask; }
    }

    [GraphNode(Category = ""Convert"")]
    public sealed partial class HarnessIntToString : RuntimeNode
    {
        public InputPort<int> Value = null!;
        public OutputPort<string> Result = null!;
        partial void InitializePorts() => Result = new OutputPort<string>(() => Value.Read().ToString());
    }
}
";

const string PackageAttr = @"
using Scaffold.GraphFlow;
using HarnessFixture;
[assembly: GraphPackage(
    Runner = typeof(HarnessRunner),
    Extension = ""harness"",
    AssetMenu = ""GraphFlow/Harness Smoke"",
    Convention = PortConvention.AllFieldsIn,
    RegistryNamespace = ""HarnessFixture.Generated"",
    DispatcherBase = typeof(HarnessDispatcherBase<,>),
    CommandBase = typeof(HarnessCommand<>))]
";

var failures = new List<string>();
RunFixture("Runtime", "HarnessFixture",        snapshotsRoot, update, failures);
RunFixture("Editor",  "HarnessFixture.Editor", snapshotsRoot, update, failures);

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
        new[]
        {
            CSharpSyntaxTree.ParseText(SyntheticSource),
            CSharpSyntaxTree.ParseText(PackageAttr),
        },
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
