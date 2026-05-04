using NUnit.Framework;

namespace Scaffold.Benchmarks.Maps
{
    /// <summary>
    /// Per-assembly LogAssert noise suppression for the Maps benchmark suite.
    /// Save/restore is delegated to <see cref="BenchSetup"/> so failing tests don't leak state
    /// into other test assemblies that run later in the same Unity test session.
    /// </summary>
    [SetUpFixture]
    public sealed class MapsBenchmarksAssemblySetup
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => BenchSetup.EnterAssembly();

        [OneTimeTearDown]
        public void OneTimeTearDown() => BenchSetup.ExitAssembly();
    }
}
