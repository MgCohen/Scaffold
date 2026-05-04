using NUnit.Framework;

namespace Scaffold.Benchmarks.States
{
    /// <summary>
    /// Per-assembly LogAssert noise suppression for the States benchmark suite.
    /// Save/restore is delegated to <see cref="BenchSetup"/>.
    /// </summary>
    [SetUpFixture]
    public sealed class StatesBenchmarksAssemblySetup
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => BenchSetup.EnterAssembly();

        [OneTimeTearDown]
        public void OneTimeTearDown() => BenchSetup.ExitAssembly();
    }
}
