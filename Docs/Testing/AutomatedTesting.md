# Automated Testing

## Purpose

This guide defines how to create effective automated tests for this Unity project: what to cover, how to structure tests, setup patterns, and best practices.

For test execution commands and CI/local quality gate behavior, see [Testing.md](Testing.md). For script internals (Unity CLI quoting via `lib/UnityProcess.ps1`, `dotnet` path quoting in `check-analyzers.ps1`, pragma gate without `.git`), see `Testing.md` → "Implementation notes".

## Testing Strategy (What To Cover)

Use a practical test pyramid:

- Many fast EditMode tests for domain and module behavior (plain C# where the code under test is agnostic; otherwise minimal Unity setup).
- Fewer integration-style EditMode tests for wiring between modules.
- Small, targeted PlayMode tests for scene/bootstrap/runtime behavior.

Prioritize coverage in this order:

1. Core game rules and state transitions (`Core/*` assemblies and services that are testable without heavy scene setup).
2. Public module contracts and invariants (constructor guards, domain events, value objects).
3. App wiring and composition boundaries (DI/bootstrap/navigation flow).
4. Critical runtime scene behavior (smoke path and fatal-log detection).
5. Regression paths for discovered bugs.

Do not spend most of coverage on visual assertions or fragile hierarchy checks.

## Layer-Specific Guidance

### Core and Domain Modules

- Prefer plain NUnit tests.
- Avoid Unity scene/object setup unless truly needed.
- Assert behavior, not implementation details.
- Test boundary inputs: min/max, empty, invalid, repeated calls.

Example in repo (add or adjust paths as your test tree grows):

- `Assets/Packages/com.scaffold.entities/Tests/Runtime/` (runtime assembly tests, when `.cs` tests are present)
- `Assets/Packages/com.scaffold.entities/Tests/Editor/` (editor assembly tests, when `.cs` tests are present)
- `Assets/Packages/com.scaffold.appflow/Tests/Editor/` (when `.cs` tests are present)

### Infra/App EditMode Tests

- Test ViewModel/controller behavior with fakes/stubs.
- Keep MonoBehaviour use minimal and explicit.
- Clean up created `GameObject` instances in teardown/dispose.

Example in repo (when present):


### PlayMode Tests

- Keep to critical end-to-end checks only.
- Use frame/time bounded waits.
- Capture Unity error/assert/exception logs and fail the test if any occur.

## How To Set Up A New Test Assembly

Create test assemblies per module in a local `Tests/` folder next to the module:

1. Add `ModuleName.Tests.asmdef`.
2. Reference only the module under test and required boundary/runtime dependencies.
3. Add `"optionalUnityReferences": ["TestAssemblies"]`.
4. For EditMode tests, set `"includePlatforms": ["Editor"]`.
5. For PlayMode tests, use no `includePlatforms` restriction unless required.
6. Keep `autoReferenced` disabled and declare explicit references.

Use existing asmdefs as templates:

- `Assets/Packages/com.scaffold.appflow/Tests/Editor/Scaffold.AppFlow.Tests.asmdef`
- `Assets/Packages/com.scaffold.entities/Tests/Runtime/Scaffold.Entities.Tests.asmdef`
- `Assets/Packages/com.scaffold.entities/Tests/Editor/Scaffold.Entities.Editor.Tests.asmdef`

## Test Design Checklist

Each test should have:

- Clear naming: `Method_WhenCondition_ExpectedResult`.
- Arrange/Act/Assert structure.
- One behavior focus per test.
- Deterministic setup (no hidden global state).
- Tight assertions with meaningful failure messages for critical checks.

Prefer:

- Fakes/stubs over real infrastructure.
- Explicit builders/helpers for readability.
- Event/property assertions when state notifications matter.
- For analyzer unit tests that depend on assembly/source topology, use shared structural fixtures (`StructuralTestGraph`) instead of one-off temporary workspace wiring.

Avoid:

- Sleeping/time-based flakiness.
- Depending on test execution order.
- Over-asserting unrelated fields in the same test.

## Regression Test Workflow (Required For Bug Fixes)

1. Write or update a test that reproduces the bug.
2. Confirm the test fails before the code fix.
3. Implement the fix.
4. Confirm the same test now passes.
5. Run the full milestone gate (`.agents/scripts/validate-changes.cmd`) and resolve all failures.

Regression template:

```csharp
[Test]
public void Finish_CalledMultipleTimes_RaisesCompletedOnlyOnce()
{
    // Arrange
    Game game = CreateGame();
    int callCount = 0;
    game.Completed += _ => callCount++;

    // Act
    game.Start();
    game.Finish();
    game.Finish();

    // Assert
    Assert.AreEqual(1, callCount);
}
```

## Coverage Goals (Practical Targets)

Use goals to guide quality decisions, not to game metrics:

- High coverage for pure domain services and value objects.
- Medium coverage for integration/wiring paths.
- Focused smoke/regression coverage for PlayMode.
- Every production bug gets a permanent regression test.

## Capturing Line/Branch Coverage

Coverage is not part of `.agents/scripts/validate-changes.cmd`. Run coverage only through the dedicated audit script.

- Package dependency: `com.unity.testtools.codecoverage` (in `Packages/manifest.json`).
- Default output directory: `Coverage/`.
- Cobertura report path: `Coverage/Report/Cobertura.xml` (used for line/branch summary).

Coverage audit controls on `.agents/scripts/run-coverage-audit.ps1`:

- `-CoverageResultsPath "<path>"` to change output location.
- `-CoverageAssemblyFilters "+Scaffold.*,+Scaffold.*"` to control assembly inclusion.

## Test Quality Check Structure (v1)

Use multiple metrics together. Do not treat one number as "test quality."

Quality dimensions for each module:

1. **Behavior/Risk Coverage**: critical behaviors, failure paths, and regressions are tested.
2. **Structural Coverage**: line/branch coverage is used to find untested code, not as a vanity target.
3. **Test Density**: tests and test code grow with runtime complexity.
4. **API/Contract Coverage**: public module APIs have direct behavioral tests.
5. **Reliability**: tests are deterministic and low-flake.

### Operational Metric Definitions

Use these definitions consistently:

- `LineCoverage`: `% of executable lines hit by tests`.
- `BranchCoverage`: `% of decision branches hit by tests`.
- `ChangeCoverage`: `% of changed lines/branches exercised in the current change`.
- `TestsPer1kLoC`: `([Test] + [UnityTest]) / (runtime LOC / 1000)`.
- `TestCodeRatio`: `test LOC / runtime LOC`.
- `ApiCoverage`: `public API symbols with direct tests / total public API symbols`.
  - For this repository, "public API symbols" means module-facing boundary APIs (for example interfaces/types under `Runtime/Contracts/`) and stable public runtime surface intended for consumers.
  - "direct tests" means at least one test that asserts behavior of that symbol, not only incidental coverage through another path.

### Suggested Thresholds (Initial)

Use these as health targets, then tune with real data:

- `LineCoverage`: `>= 70%` for Core/Infra modules, `>= 60%` for App/Tooling modules.
- `BranchCoverage`: `>= 60%` for Core/Infra modules, `>= 50%` for App/Tooling modules.
- `TestsPer1kLoC`: warning under `8`; target `10+` for complex modules.
- `TestCodeRatio`: warning under `0.5`; target `0.7+` for complex modules.
- `ApiCoverage`: `>= 90%` for boundary APIs in Core/Infra, `>= 80%` elsewhere.
- `RegressionCoverage`: `100%` of production bugs must map to a permanent regression test.

### Per-Module Quality Check (Scorecard)

Use this checklist in milestone reviews:

- Module:
- Runtime LOC:
- Test LOC:
- Test count (`[Test]` + `[UnityTest]`):
- TestsPer1kLoC:
- TestCodeRatio:
- LineCoverage:
- BranchCoverage:
- ApiCoverage:
- Critical behaviors covered (list):
- Failure/guard paths covered (list):
- Regression tests linked to known bugs (list):
- Flaky tests in last 30 runs (list or `none`):
- Quality decision: `PASS`, `PASS WITH RISK`, or `FAIL`.
- Required actions before completion:

## Repository coverage baseline

This scaffold does not ship a frozen per-module test count table. After you add or expand tests, refresh priorities from real `[Test]` / `[UnityTest]` counts and runtime LOC (see `Plans/ModuleTestCoverage/ModuleTestCoverage-ExecPlan.md` if present).

Suggested focus areas for new coverage (tune to your tree):

- `Infra/Navigation`, `Infra/MVVM`, `Infra/SceneFlow` (wiring-heavy).
- `Core/Entities`, application composition / startup (`AppFlowRoot` in your game assembly).
- `Tools/*` (pure logic where applicable).

## Allowed Low-Test Module Exceptions

In rare cases, a module may intentionally keep minimal tests (for example, marker/compatibility assemblies with no meaningful runtime branching logic).

Rules for exceptions:

- The exception must be documented in that module's doc under `Docs/` with a clear rationale.
- The module must keep at least one smoke/contract test so it remains part of automated test discovery.
- The module doc must define trigger conditions that require expanding test coverage.
- If module scope expands (new behavior, branching logic, stateful flow, or new public API), the exception no longer applies and test coverage must be increased.

Current documented example:

- `Docs/Tools/Records.md` (`Scaffold.Records` marker compatibility module).

## Common Pitfalls

- Writing mostly PlayMode tests for logic that belongs in pure C#.
- Tests coupled to private implementation instead of contract behavior.
- Missing cleanup for created Unity objects.
- Large tests with multiple unrelated assertions and failure causes.
- Skipping analyzer checks after test changes.

## Research Foundations

This structure is based on widely cited guidance:

- Coverage is useful for finding gaps, but not sufficient as a standalone quality metric:
  - Martin Fowler, [Test Coverage](https://martinfowler.com/bliki/TestCoverage.html)
  - Google Testing Blog, [Code coverage best practices](https://testing.googleblog.com/2020/08/code-coverage-best-practices.html)
- Practical threshold framing and caution against metric gaming:
  - Microsoft, [.NET unit testing code coverage](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage)
- API/contract behavior should be validated at boundaries:
  - Martin Fowler, [Consumer-Driven Contracts](https://martinfowler.com/articles/consumerDrivenContracts.html)
  - OpenAPI Initiative, [OpenAPI Specification](https://swagger.io/specification/)
