using NUnit.Framework;

namespace Scaffold.Benchmarks.GraphFlow
{
    [SetUpFixture]
    public sealed class GraphFlowBenchmarksAssemblySetup
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => BenchSetup.EnterAssembly();

        [OneTimeTearDown]
        public void OneTimeTearDown() => BenchSetup.ExitAssembly();
    }
}
