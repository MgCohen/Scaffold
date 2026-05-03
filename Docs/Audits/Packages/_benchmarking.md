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

Benchmarks live **outside** the per-package UPM tree, under a single repo-internal folder:

```text
Assets/Benchmarks/
  Bench/                                 # canonical Bench.Measure + BenchSetup helper
    Bench.cs
    BenchSetup.cs
    Scaffold.Benchmarks.asmdef           # Editor-only, references Unity.PerformanceTesting + TestAssemblies
  <PackageShortName>/
    *Benchmarks.cs                       # one fixture per scenario family
    Scaffold.Benchmarks.<PackageShortName>.asmdef
    <PackageShortName>BenchmarksAssemblySetup.cs   # [SetUpFixture] per assembly
    baselines.json
```

Why outside the package tree: perf tests are dev/CI artifacts, not consumer code. Putting them under `Assets/Benchmarks/` (instead of `Assets/Packages/<pkg>/Tests/Performance/`) keeps the UPM surface lean — package consumers receive runtime + unit tests + samples and nothing else. Each per-package perf asmdef references the canonical `Scaffold.Benchmarks` plus the package under test plus `Unity.PerformanceTesting`. Each asmdef sets `includePlatforms: [Editor]` so the benches never compile into player builds.

---

## The helper (`Bench.Measure`)

Canonical implementation lives in `Assets/Benchmarks/Bench/Bench.cs` (namespace `Scaffold.Benchmarks`). Reference it from each package's perf asmdef; do **not** copy it. The shipped version reports six sample groups per measurement:

| Sample group   | Source                                                                            | Notes |
|----------------|-----------------------------------------------------------------------------------|-------|
| `Time`         | `Stopwatch`                                                                       | ns/op |
| `Allocated`    | First counter that probes as working: `GC.GetAllocatedBytesForCurrentThread()` → `GC.GetTotalMemory(false)` delta | Bytes/op. Reads 0 on runtimes where neither advances. (`GC.GetTotalAllocatedBytes` would be ideal but is not exposed by Unity 6's Mono BCL.) |
| `AllocCount`   | `UnityEngine.Profiling.Recorder.Get("GC.Alloc")` filtered to current thread       | Number of `GC.Alloc` events/op. Works in EditMode batchmode regardless of Mono's per-thread heap-counter quirks. |
| `Gen0`/`Gen1`/`Gen2` | `GC.CollectionCount(gen)` deltas                                            | Collections per measurement window. |

Why two byte-counter candidates and a marker-count fallback? `GC.GetAllocatedBytesForCurrentThread()` returns 0 on several Unity Editor / Mono configurations (notably batchmode), which silently zeroes the `Allocated` column for every test. The `GC.Alloc` Profiler-marker count is the always-available fallback signal — fewer details (no bytes), but it never lies about whether code allocated.

Selection happens once in `Bench`'s static ctor and is exposed via `Bench.ByteSource` / `Bench.BytesCounterWorks` so verification tests can call `Assert.Ignore` instead of asserting against a known-broken counter.

```csharp
[Test, Performance]
public void Foo()
{
    Bench.Measure(() => DoWork()); // 6 sample groups, names prefixed with the test method name
}

[Test]
public void Foo_HotPath_NoAlloc()
{
    Bench.NoAllocations(() => DoWork()); // bytes when available, else GC.Alloc count
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

- **Byte-counter availability varies by runtime.** `Bench.ByteSource` records which counter the harness picked. If it resolves to `None`, `Allocated` will read 0 in the CSV — fall back to `AllocCount` and Gen0/1/2, or run the suite under PlayMode / IL2CPP. The first run of `Harness_ReportsByteCounterChoice` in `PerformanceAllocationVerificationTests` prints the choice so you can spot-check.
- **`GC.GetTotalMemory(false)` fallback is heap-size, not cumulative.** When the harness lands on this counter, allocations larger than Gen0's threshold inside the measurement window will trigger a collection and produce a negative delta — clamped to 0. If this happens, `AllocCount` is the trustworthy signal.
- **`AllocCount` is filtered to the current managed thread.** If the operation under test schedules work on another thread (uncommon in `entities`/`states`), you'll under-count. Same caveat applies to `GC.GetAllocatedBytesForCurrentThread`. Process-wide `GC.GetTotalAllocatedBytes` does not have this limitation.
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
| `Tests/Performance/Bdn/` | `Assets/Benchmarks/<PackageShortName>/` |
| `Assets/Packages/<pkg>/Tests/Performance/` | `Assets/Benchmarks/<PackageShortName>/` |
| `[Benchmark] public void Foo()` | `[Test, Performance] public void Foo() { Bench.Measure(() => ...); }` |
| Implicit Gen0/Gen1/Gen2 | Reported automatically by `Bench.Measure` |
| `Allocated` column | `Allocated` sample group |

The `entities`, `states`, and `entities.states` audit plans have been updated in place to drop BenchmarkDotNet references.

---

## Reporting & CI

- **Local:** Test Runner → results pane — often **CSV** (or table) export for viewing. For **JSON**, use batchmode **`-perfTestResults <path>.json`** (see [Performance testing command-line arguments](https://docs.unity3d.com/Packages/com.unity.test-framework.performance@3.2/manual/cmd-line-args.html)).
- **CI:** `unity -batchmode -runTests … -perfTestResults results.json` (and `-testResults` for NUnit XML). Optionally `-testCategory "Performance"`. Feed JSON to [PerformanceBenchmarkReporter](https://github.com/Unity-Technologies/PerformanceBenchmarkReporter) for over-time HTML reports.
- **Baselines:** keep a checked-in `baselines.json` per package under `Assets/Benchmarks/<PackageShortName>/` and diff against it in CI. PR fails if any sample group exceeds the policy thresholds above.
