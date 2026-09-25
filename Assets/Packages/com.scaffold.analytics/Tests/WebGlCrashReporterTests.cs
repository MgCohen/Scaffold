using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Scaffold.Analytics.Tests
{
    [TestFixture]
    public sealed class WebGlCrashReporterTests
    {
        [Test]
        public void WebGlLibrary_ExportsDiagnosticStateToEmscriptenRuntime()
        {
            string[] assetGuids = AssetDatabase.FindAssets("ScaffoldAnalyticsWebGlDiagnostics");
            string libraryPath = string.Empty;
            foreach (string assetGuid in assetGuids)
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (candidatePath.EndsWith("ScaffoldAnalyticsWebGlDiagnostics.jslib", System.StringComparison.Ordinal))
                {
                    libraryPath = candidatePath;
                    break;
                }
            }

            Assert.That(libraryPath, Is.Not.Empty);
            string librarySource = File.ReadAllText(Path.GetFullPath(libraryPath));
            Assert.That(librarySource, Does.Contain(".$ScaffoldAnalyticsWebGlDiagnostics = {"));
            Assert.That(librarySource, Does.Contain(
                "autoAddDeps(ScaffoldAnalyticsWebGlDiagnosticsLibrary, \"$ScaffoldAnalyticsWebGlDiagnostics\")"));
            Assert.That(librarySource, Does.Not.Contain("var ScaffoldAnalyticsWebGlDiagnostics = {"));
        }

        [Test]
        public void ContainerRegistration_ResolvesReporterWithoutInternalStoreRegistration()
        {
            RecordingAnalytics analytics = new RecordingAnalytics();
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterInstance<IAnalyticsService>(analytics);
            builder.Register<WebGlCrashReporter>(Lifetime.Singleton)
                .As<IWebGlCrashReporter>();
            IObjectResolver container = builder.Build();

            IWebGlCrashReporter reporter = container.Resolve<IWebGlCrashReporter>();

            Assert.That(reporter, Is.Not.Null);
        }

        [Test]
        public void TryReportPreviousSession_WhenPreviousSessionEndedAbruptly_RecordsFlushesAndAcknowledges()
        {
            RecordingAnalytics analytics = new RecordingAnalytics();
            StubReportStore store = new StubReportStore(BuildReport(true, "session-a", "webgl_context_lost"));
            WebGlCrashReporter reporter = new WebGlCrashReporter(analytics, store);

            bool reported = reporter.TryReportPreviousSession();

            Assert.That(reported, Is.True);
            Assert.That(analytics.Events, Has.Count.EqualTo(1));
            Assert.That(analytics.Events[0], Is.TypeOf<WebGlAbruptSessionEvent>());
            Assert.That(analytics.FlushCount, Is.EqualTo(1));
            Assert.That(store.AcknowledgedSessionIds, Is.EqualTo(new[] { "session-a" }));
            Assert.That(analytics.Events[0].Parameters["lastPhase"], Is.EqualTo("webgl_context_lost"));
            Assert.That(analytics.Events[0].Parameters["browserPlatform"], Is.EqualTo("iPhone"));
            Assert.That(analytics.Events[0].Parameters, Does.Not.ContainKey("platform"));
        }

        [Test]
        public void TryReportPreviousSession_WhenPreviousSessionEndedNormally_DoesNotRecord()
        {
            RecordingAnalytics analytics = new RecordingAnalytics();
            StubReportStore store = new StubReportStore(BuildReport(false, "session-b", "page_hidden"));
            WebGlCrashReporter reporter = new WebGlCrashReporter(analytics, store);

            bool reported = reporter.TryReportPreviousSession();

            Assert.That(reported, Is.False);
            Assert.That(analytics.Events, Is.Empty);
            Assert.That(analytics.FlushCount, Is.Zero);
            Assert.That(store.AcknowledgedSessionIds, Is.Empty);
        }

        [Test]
        public void TryReportPreviousSession_WhenCalledTwice_DoesNotDuplicateSession()
        {
            RecordingAnalytics analytics = new RecordingAnalytics();
            StubReportStore store = new StubReportStore(BuildReport(true, "session-c", "javascript_error"));
            WebGlCrashReporter reporter = new WebGlCrashReporter(analytics, store);

            bool firstResult = reporter.TryReportPreviousSession();
            bool secondResult = reporter.TryReportPreviousSession();

            Assert.That(firstResult, Is.True);
            Assert.That(secondResult, Is.False);
            Assert.That(analytics.Events, Has.Count.EqualTo(1));
            Assert.That(analytics.FlushCount, Is.EqualTo(1));
        }

        [Test]
        public void TryReportPreviousSession_WhenFlushFails_LeavesSessionPendingForNextLaunch()
        {
            RecordingAnalytics analytics = new RecordingAnalytics { ThrowOnFlush = true };
            StubReportStore store = new StubReportStore(BuildReport(true, "session-retry", "analytics_flush_failed"));
            WebGlCrashReporter reporter = new WebGlCrashReporter(analytics, store);
            LogAssert.Expect(LogType.Error, new Regex("Failed to recover the previous WebGL session"));

            bool reported = reporter.TryReportPreviousSession();

            Assert.That(reported, Is.False);
            Assert.That(analytics.Events, Has.Count.EqualTo(1));
            Assert.That(store.AcknowledgedSessionIds, Is.Empty);
        }

        [Test]
        public void TryReportPreviousSession_WhenMessageIsOversized_TruncatesAnalyticsParameter()
        {
            string oversizedMessage = new string('x', 180);
            RecordingAnalytics analytics = new RecordingAnalytics();
            StubReportStore store = new StubReportStore(
                BuildReport(true, "session-d", "console_error", oversizedMessage));
            WebGlCrashReporter reporter = new WebGlCrashReporter(analytics, store);

            reporter.TryReportPreviousSession();

            string recordedMessage = (string)analytics.Events[0].Parameters["lastErrorMessage"];
            Assert.That(recordedMessage, Has.Length.EqualTo(100));
        }

        [Test]
        public void TryReportPreviousSession_WhenWebGlContextIsLost_RecordsStatusMessage()
        {
            RecordingAnalytics analytics = new RecordingAnalytics();
            StubReportStore store = new StubReportStore(
                BuildReport(true, "session-context", "webgl_context_lost", "GPU reset", "statusMessage"));
            WebGlCrashReporter reporter = new WebGlCrashReporter(analytics, store);

            reporter.TryReportPreviousSession();

            Assert.That(analytics.Events[0].Parameters["lastErrorMessage"], Is.EqualTo("GPU reset"));
        }

        private static string BuildReport(
            bool endedAbruptly,
            string sessionId,
            string lastPhase,
            string message = "context lost",
            string detailName = "message")
        {
            string abruptValue = endedAbruptly ? "true" : "false";
            string cleanExitValue = endedAbruptly ? "false" : "true";
            return "{\"previousEndedAbruptly\":" + abruptValue +
                ",\"previous\":{\"sessionId\":\"" + sessionId +
                "\",\"startedAt\":1000,\"updatedAt\":4500,\"phase\":\"" + lastPhase +
                "\",\"cleanExit\":" + cleanExitValue +
                ",\"runtimeReady\":true,\"events\":[{\"at\":4500,\"phase\":\"" + lastPhase +
                "\",\"details\":{\"" + detailName + "\":\"" + message +
                "\"}}],\"environment\":{\"userAgent\":\"Mobile Safari\",\"platform\":\"iPhone\"," +
                "\"viewport\":\"390x635\",\"devicePixelRatio\":3,\"wasmHeapBytes\":268435456}}}";
        }

        private sealed class RecordingAnalytics : IAnalyticsService
        {
            public List<AnalyticsEvent> Events { get; } = new List<AnalyticsEvent>();

            public int FlushCount { get; private set; }

            public bool ThrowOnFlush { get; set; }

            public void Record<T>(T evt) where T : AnalyticsEvent
            {
                Events.Add(evt);
            }

            public void Flush()
            {
                FlushCount++;
                if (ThrowOnFlush)
                {
                    throw new System.InvalidOperationException("Simulated Analytics flush failure.");
                }
            }
        }

        private sealed class StubReportStore : IWebGlCrashReportStore
        {
            public StubReportStore(string report)
            {
                Report = report;
            }

            public bool IsAvailable => true;

            public string Report { get; }

            public List<string> AcknowledgedSessionIds { get; } = new List<string>();

            public void Acknowledge(string sessionId)
            {
                AcknowledgedSessionIds.Add(sessionId);
            }

            public string ReadReport()
            {
                return Report;
            }

            public void SetCheckpoint(string phase, string details)
            {
            }
        }
    }
}
