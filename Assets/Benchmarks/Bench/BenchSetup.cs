#nullable enable
using UnityEngine.TestTools;

namespace Scaffold.Benchmarks
{
    /// <summary>
    /// Shared setup used by per-package <c>[SetUpFixture]</c> shims so they can drop unrelated
    /// EditMode batchmode log noise (netcode / multiplayer connect failures, etc.) without
    /// duplicating the save/restore dance for <see cref="LogAssert.ignoreFailingMessages"/>.
    /// Each per-package benchmark assembly hosts a tiny shim that calls
    /// <see cref="EnterAssembly"/> in <c>[OneTimeSetUp]</c> and <see cref="ExitAssembly"/> in
    /// <c>[OneTimeTearDown]</c>. Per-test re-arm is exposed via <see cref="RearmPerTest"/>.
    /// </summary>
    public static class BenchSetup
    {
        static bool? previousIgnoreFailingMessages;

        public static void EnterAssembly()
        {
            previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
        }

        public static void ExitAssembly()
        {
            if (previousIgnoreFailingMessages.HasValue)
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages.Value;
                previousIgnoreFailingMessages = null;
            }
        }

        public static void RearmPerTest() => LogAssert.ignoreFailingMessages = true;
    }
}
