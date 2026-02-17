using NUnit.Framework;
using System;
using Scaffold.Logging;
using UnityEngine;
using UnityEngine.TestTools;

namespace Scaffold.Tests.Logging
{
    public class LoggerAssertTests
    {
        [SetUp]
        public void Setup()
        {
            GameDebug.Initialize(false);
        }

        [Test]
        public void AssertThat_WhenConditionTrue_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => { GameDebug.AssertThat(true, "Should not fail"); });
        }

        [Test]
        public void AssertThat_WhenConditionFalse_ThrowsException()
        {
            LogAssert.Expect(
                LogType.Error,
                "[Client][Error][Assert] Health must be > 0"
            );

            Assert.Throws<ApplicationException>(() => { GameDebug.AssertThat(false, "Health must be > 0"); });
        }
    }
}