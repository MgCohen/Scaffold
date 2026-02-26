using Scaffold.Logging;
using NUnit.Framework;

namespace Scaffold.Tests.Logging
{
    public class LoggerBasicTests
    {
        [SetUp]
        public void Setup()
        {
            GameDebug.Initialize(isServer: false);
        }

        [Test]
        public void LogClient_BasicMessage_FormatsCorrectly()
        {
            string result = GameDebug.LogClient("Hello");

            Assert.That(result, Does.Contain("[Client]"));
            Assert.That(result, Does.Contain("[Log]"));
            Assert.That(result, Does.Contain("Hello"));
        }

        [Test]
        public void LogClient_WithKeys_FormatsKeysCorrectly()
        {
            string result = GameDebug.LogClient(
                "Player connected",
                LogKey.Player,
                42);

            Assert.That(result, Does.Contain("[Player][42]"));
            Assert.That(result, Does.Contain("Player connected"));
        }

        [Test]
        public void LogClient_NullMessage_IsHandled()
        {
            string result = GameDebug.LogClient(null);

            Assert.That(result, Does.Contain("null"));
        }
    }
}