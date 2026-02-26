using Scaffold.Logging;
using NUnit.Framework;

namespace Scaffold.Tests.Logging
{
    public class LoggerSharedEnvironmentTests
    {
        [Test]
        public void LogShared_WhenClient_UsesClientEnvironment()
        {
            GameDebug.Initialize(false);

            string result = GameDebug.Log("Test");

            Assert.That(result, Does.Contain("[Client]"));
        }

        [Test]
        public void LogShared_WhenServer_UsesServerEnvironment()
        {
            GameDebug.Initialize(true);

            string result = GameDebug.Log("Test");

            Assert.That(result, Does.Contain("[Server]"));
        }
    }
}