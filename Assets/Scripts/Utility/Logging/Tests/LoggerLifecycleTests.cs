using Scaffold.Logging;
using NUnit.Framework;

namespace Scaffold.Tests.Logging
{
    public class LoggerLifecycleTests
    {
        [SetUp]
        public void Setup()
        {
            GameDebug.Initialize(false);
        }

        [Test]
        public void LogClientStarting_AddsStartingKey()
        {
            string result = GameDebug.LogClientStarting(LogKey.Network);

            Assert.That(result, Does.Contain("[Starting]"));
            Assert.That(result, Does.Contain("[Network]"));
        }

        [Test]
        public void LogClientInitialized_OnlyImplicitKey()
        {
            string result = GameDebug.LogClientInitialized();

            Assert.That(result, Does.Contain("[Initialized]"));
        }
    }
}