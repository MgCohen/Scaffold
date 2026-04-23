using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Scaffold.AppFlow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Scaffold.AppFlow.Tests.Editor
{
    [TestFixture]
    public sealed class AppFlowErrorHandlerTests
    {
        [Test]
        public void SameException_ReportedTwiceWithDifferentPhases_LogsOnce()
        {
            var handler = new AppFlowErrorHandler();
            var ex = new InvalidOperationException("boom");
            LogAssert.Expect(LogType.Error, new Regex(@"\[AppFlow\]"));
            handler.Report(new AppFlowErrorInfo(AppFlowErrorPhase.Init, "MyLayer", "t1", ex, DateTime.UtcNow));
            handler.Report(new AppFlowErrorInfo(AppFlowErrorPhase.Startup, null, "t2", ex, DateTime.UtcNow));
            IReadOnlyList<AppFlowErrorInfo> recent = handler.Recent;
            Assert.That(recent.Count, Is.EqualTo(2));
            Assert.That(recent[0].Phase, Is.EqualTo(AppFlowErrorPhase.Init));
            Assert.That(recent[1].Phase, Is.EqualTo(AppFlowErrorPhase.Startup));
            Assert.That(recent[0].Exception, Is.SameAs(recent[1].Exception));
        }

        [Test]
        public void Manual_Report_IsRecordedAndRaisesOnError()
        {
            var handler = new AppFlowErrorHandler();
            var captured = new List<AppFlowErrorInfo>();
            handler.OnError += info => captured.Add(info);
            var ex = new Exception("manual");
            LogAssert.Expect(LogType.Error, new Regex(@"\[AppFlow\].*Manual"));
            handler.Report("unit", ex);
            Assert.That(captured.Count, Is.EqualTo(1));
            Assert.That(captured[0].Phase, Is.EqualTo(AppFlowErrorPhase.Manual));
            Assert.That(captured[0].Exception, Is.SameAs(ex));
            Assert.That(handler.Recent.Count, Is.EqualTo(1));
        }

        [Test]
        public void OnError_SubscriberThrows_DoesNotBreakHandler()
        {
            var handler = new AppFlowErrorHandler();
            handler.OnError += _ => throw new InvalidOperationException("subscriber boom");
            LogAssert.Expect(LogType.Error, new Regex(@"\[AppFlow\]"));
            LogAssert.Expect(LogType.Error, new Regex("subscriber boom"));
            handler.Report("t", new Exception("original"));
        }
    }
}
