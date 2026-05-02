using NUnit.Framework;
using UnityEngine.TestTools;

namespace Scaffold.Maps.Tests.Performance
{
    /// <summary>
    /// Suppresses unrelated error logs (e.g. netcode / multiplayer service connection failures
    /// that fire during EditMode batchmode) from failing perf tests. Scoped to this assembly.
    /// Set both in OneTimeSetUp and in BeforeTest because Unity's per-test log handler
    /// re-evaluates the flag at the start of each test.
    /// </summary>
    [SetUpFixture]
    public sealed class PerformanceAssemblySetup
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            LogAssert.ignoreFailingMessages = true;
        }
    }

    /// <summary>
    /// Per-test re-arm so a test framework reset between tests cannot reintroduce log-failure noise.
    /// Inherited via NUnit's TestActionAttribute mechanism would be cleaner but adds a dependency;
    /// the three perf test classes opt in by adding <see cref="SetUp"/> directly.
    /// </summary>
    public static class PerfLogNoise
    {
        public static void Suppress() => LogAssert.ignoreFailingMessages = true;
    }
}
