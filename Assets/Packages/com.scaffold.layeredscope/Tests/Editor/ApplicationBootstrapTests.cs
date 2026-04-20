using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.LayeredScope;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Scaffold.LayeredScope.Tests.Editor
{
    [TestFixture]
    public sealed class ApplicationBootstrapTests
    {
        [Test]
        public async Task OnStartupFailedAsync_InvokedWhenInitialLayerInitializableThrows()
        {
            var go = new GameObject("BootstrapTest");
            go.SetActive(false);
            try
            {
                var bootstrap = go.AddComponent<RecordingBootstrap>();
                go.SetActive(true);

                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Push failed for 'ThrowingInitLayer'"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("\\[ApplicationBootstrap\\] Startup failed"));

                InvokeStart(bootstrap);

                Exception faulted = null;
                try
                {
                    await bootstrap.ReadyTask;
                }
                catch (Exception ex)
                {
                    faulted = ex;
                }

                Assert.That(faulted, Is.Not.Null, "ReadyTask should have faulted.");
                Assert.That(bootstrap.StartupFailureCalled, Is.True, "OnStartupFailedAsync must be invoked on failure.");
                Assert.That(bootstrap.StartupFailureException, Is.SameAs(faulted));
            }
            finally
            {
                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }

        private static void InvokeStart(ApplicationBootstrap bootstrap)
        {
            MethodInfo start = typeof(ApplicationBootstrap).GetMethod(
                "Start",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(start, Is.Not.Null, "ApplicationBootstrap.Start must exist.");
            start.Invoke(bootstrap, Array.Empty<object>());
        }

        private sealed class RecordingBootstrap : ApplicationBootstrap
        {
            public bool StartupFailureCalled { get; private set; }
            public Exception StartupFailureException { get; private set; }

            protected override IEnumerable<IScopeLayer> GetInitialLayers()
            {
                yield return new ThrowingInitLayer();
            }

            protected override Task OnStartupFailedAsync(Exception ex, CancellationToken ct)
            {
                StartupFailureCalled = true;
                StartupFailureException = ex;
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingInitLayer : IScopeLayer
        {
            public void Install(IContainerBuilder builder)
            {
                builder.Register<ThrowingInitializable>(Lifetime.Singleton).As<IAsyncInitializable>();
            }

            private sealed class ThrowingInitializable : IAsyncInitializable
            {
                public Task InitializeAsync(CancellationToken ct)
                {
                    throw new InvalidOperationException("startup boom");
                }
            }
        }

    }
}
