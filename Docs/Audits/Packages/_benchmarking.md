# Benchmarking — tooling, helper, and run policy

Single source of truth for how every `## Benchmark plan` section in the per-package audits should be implemented.

The audit pre-existing version of the plans cited a mix of `Unity.PerformanceTesting` and `BenchmarkDotNet`. We have since consolidated on **Unity.PerformanceTesting only**, for the reasons below. Where a plan still references `BenchmarkDotNet` / `BDN` / `[MemoryDiagnoser]` / `Tests/Performance/Bdn/` it is stale — replace with the helper + path described here.

---

## Why a single tool

- **One CI lane, one results format, one mental model.** All benchmarks run from Unity Test Runner; results are JSON consumable by [PerformanceBenchmarkReporter](https://github.com/Unity-Technologies/PerformanceBenchmarkReporter) for over-time tracking.
- **Tests live next to the package they test** — same convention as the unit suite.
- **Real target devices.** Unity Test Runner can run PlayMode tests on Android / iOS / consoles. BenchmarkDotNet is desktop-only.
- **You measure what you ship: IL2CPP.** BenchmarkDotNet runs against CoreCLR or desktop Mono, not IL2CPP, so its numbers are not representative of game runtime.

The convenience gap (`[MemoryDiagnoser]` auto-reports bytes + Gen0/1/2) is closed by one ~30-line helper described below. Net trade: small loss of sub-50 ns statistical rigor; large win on device coverage and runtime correctness.

---

## Tests location

```text
Assets/Packages/<package-name>/Tests/Performance/
```

Use a separate asmdef from the unit-test asmdef (or a define constraint such as `UNITY_INCLUDE_PERFORMANCE_TESTS`) so the perf suite doesn't run in the regular unit-test pass.

---

## The helper (`Bench.Measure`)

Drop this once into a shared `Tests/Performance/Bench.cs` (or a small dedicated package, e.g. `com.scaffold.testing.benchmarks`). Every test then becomes a one-liner.

```csharp
using System;
using Unity.PerformanceTesting;

public static class Bench
{
    static readonly SampleGroup Time  = new("Time",      SampleUnit.Nanosecond);
    static readonly SampleGroup Bytes = new("Allocated", SampleUnit.Byte);
    static readonly SampleGroup Gen0  = new("Gen0",      SampleUnit.Undefined);
    static readonly SampleGroup Gen1  = new("Gen1",      SampleUnit.Undefined);
    static readonly SampleGroup Gen2  = new("Gen2",      SampleUnit.Undefined);

    /// <summary>
    /// Per-op time and allocation profile. Reports five sample groups:
    /// Time (ns/op), Allocated (bytes/op), Gen0/Gen1/Gen2 (collections per measurement window).
    /// </summary>
    public static void Measure(Action action,
                               int warmup = 10,
                               int measurements = 20,
                               int iterationsPer = 1000)
    {
        for (int i = 0; i < warmup; i++) action();

        for (int m = 0; m < measurements; m++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long b0  = GC.GetAllocatedBytesForCurrentThread();
            int  g00 = GC.CollectionCount(0);
            int  g10 = GC.CollectionCount(1);
            int  g20 = GC.CollectionCount(2);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterationsPer; i++) action();
            sw.Stop();

            long bytesPerOp = (GC.GetAllocatedBytesForCurrentThread() - b0) / iterationsPer;
            double nsPerOp  = sw.Elapsed.TotalMilliseconds * 1_000_000.0 / iterationsPer;

            Performance.Measure.Custom(Time,  nsPerOp);
            Performance.Measure.Custom(Bytes, bytesPerOp);
            Performance.Measure.Custom(Gen0,  GC.CollectionCount(0) - g00);
            Performance.Measure.Custom(Gen1,  GC.CollectionCount(1) - g10);
            Performance.Measure.Custom(Gen2,  GC.CollectionCount(2) - g20);
        }
    }

    /// <summary>
    /// Asserts a piece of code allocates zero bytes. Use inside `[Test]` (no `[Performance]` needed).
    /// </summary>
    public static void NoAllocations(Action action)
    {
        action(); // warm JIT, prime statics
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        if (allocated != 0)
            throw new InvalidOperationException($"Allocated {allocated} bytes; expected 0.");
    }
}
```

---

## Usage

```csharp
using NUnit.Framework;
using Unity.PerformanceTesting;

public sealed class StoreExecuteBenchmarks
{
    [Test, Performance]
    public void Execute_OneMutator_OneSlice()
    {
        var store = BuildStore();
        Bench.Measure(() => store.Execute(reference, payload));
    }

    [Test]
    public void Execute_HotPath_AllocatesNothing()
    {
        var store = BuildStore();
        Bench.NoAllocations(() => store.Execute(reference, payload));
    }
}
```

That's the whole API. Every benchmark plan entry maps to one `Bench.Measure(...)` call inside a `[Test, Performance]` method, with the scenario as setup.

---

## Run policy

Every package's benchmark suite runs in **two configurations**, and CI gates regressions on both:

1. **Editor (Mono)** — fast inner loop. Run on every PR. Catches managed-allocation regressions cheaply. Distortion: editor-only allocations (e.g. `Debug.LogWarning` formatting) show up here and disappear in player builds.
2. **PlayMode standalone IL2CPP** — the runtime your game ships on. Run on a nightly job (or per-PR if your CI budget allows). Allocation profile here is canonical; Editor is advisory.

For both: run the same test methods. The helper is platform-agnostic.

Optional third lane: **target device** (Android low-end / iOS) for the packages whose hot paths actually hit the device differently (`addressables`, `view`, `mvvm`, ad SDKs). Not required for `entities` / `states` / `entities.states` — pure C# in any runtime.

---

## Pass/fail policy

Per-test, declare success criteria explicitly. Two regression rules apply project-wide:

- **`Allocated` per op** must not regress >10 % vs the recorded baseline. If the test claims zero allocs, regression to >0 fails the PR.
- **`Gen0` collections per measurement window** must not increase. Hot paths that go from Gen0=0 to Gen0=1 fail the PR even if `Allocated` is unchanged — the cliff is the GC pause, not the bytes.
- **`Time`** regressions of >5 % flag for review but don't auto-fail (run-to-run variance is too high in Editor; tighten only if running on a dedicated bench machine).

The benchmark plan entries in the package audits state per-test what "success" looks like for the proposed refactor. Those numbers become the new baseline once the refactor lands.

---

## Caveats

- **`GC.GetAllocatedBytesForCurrentThread()` is per-thread.** If the operation under test schedules work on another thread (uncommon in `entities`/`states`), you'll under-count. Note this in the test if it applies.
- **`Stopwatch` granularity in the Editor is ~50 ns.** Operations below that need higher `iterationsPer` to dilute the floor. For sub-50 ns ops, raise `iterationsPer` to 10 000 or 100 000.
- **First measurement after `GC.Collect()` is the slowest.** That's intentional — we want a clean baseline. Outliers are tolerated by Unity.PerformanceTesting's median reporting.
- **`Bench.Measure` does not box the lambda** — `Action` is captured once and reused. Verify with the `NoAllocations` companion if you suspect harness alloc bleeding into measurements.
- **IL2CPP differences.** Generic virtual dispatch, `Dictionary<,>` resize behavior, and small-struct boxing all have IL2CPP-specific characteristics. A test green in Editor and red on IL2CPP is a real finding, not a flake.

---

## Migration from BenchmarkDotNet references in audits

If you read a benchmark plan entry citing `BenchmarkDotNet`, `[MemoryDiagnoser]`, `Bdn`, `dotnet run -c Release`, or `Tests/Performance/Bdn/`, treat it as stale and translate as follows:

| Old | New |
|---|---|
| `BenchmarkDotNet + [MemoryDiagnoser]` | `[Test, Performance]` + `Bench.Measure(() => ...)` |
| `Tests/Performance/Bdn/` | `Tests/Performance/` |
| `[Benchmark] public void Foo()` | `[Test, Performance] public void Foo() { Bench.Measure(() => ...); }` |
| Implicit Gen0/Gen1/Gen2 | Reported automatically by `Bench.Measure` |
| `Allocated` column | `Allocated` sample group |

The `entities`, `states`, and `entities.states` audit plans have been updated in place to drop BenchmarkDotNet references.

---

## Reporting & CI

- **Local:** Test Runner → results pane → JSON export.
- **CI:** `unity -batchmode -runTests -testCategory "Performance"` writes results JSON; feed to [PerformanceBenchmarkReporter](https://github.com/Unity-Technologies/PerformanceBenchmarkReporter) for the over-time HTML report.
- **Baselines:** keep a checked-in `baselines.json` per package under `Tests/Performance/` and diff against it in CI. PR fails if any sample group exceeds the policy thresholds above.
