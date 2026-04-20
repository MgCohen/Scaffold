using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.CloudCode;
using Unity.Services.CloudCode.Subscriptions;
using UnityEngine;
using VContainer;

namespace Scaffold.CloudCode.Tests
{
    public sealed class CloudCodeServiceOptimisticTests
    {
        private const string Module = "testModule";
        private const string Endpoint = "testEndpoint";

        [Test]
        [Timeout(10000)]
        public async Task OptimisticPath_ReturnsOptimisticImmediately_ThenValidateRunsWhenServerCompletes()
        {
            CloudCodeSettings settings = ScriptableObject.CreateInstance<CloudCodeSettings>();
            var serverGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var validateTcs = new TaskCompletionSource<(int ServerId, int OptimisticId)>(TaskCreationOptions.RunContinuationsAsynchronously);

            var handler = new TestOptimisticHandler
            {
                ExpectedModule = Module,
                ExpectedEndpoint = Endpoint,
                OptimisticValue = new TestResponse { Id = 1 },
                ValidateCallback = (server, optimistic) =>
                {
                    validateTcs.TrySetResult((server.Id, optimistic.Id));
                },
            };

            var registry = new CloudCodeOptimisticHandlerRegistry();
            registry.Register(handler);

            var fakeSdk = new FakeUnityCloudCodeService(async (_, _, _) =>
            {
                await serverGate.Task.ConfigureAwait(false);
                return "{\"id\":42}";
            });

            CloudCodeService service = new CloudCodeService(
                settings,
                new CloudCodeSdkCallHandler(fakeSdk),
                registry,
                new CloudCodeErrorHandler());

            Task<TestResponse> call = service.CallEndpointAsync<TestResponse>(Module, Endpoint, new TestRequest());
            TestResponse returned = await call.ConfigureAwait(false);
            Assert.That(returned.Id, Is.EqualTo(1));
            Assert.That(validateTcs.Task.IsCompleted, Is.False, "validate must not run until the server task completes");

            serverGate.TrySetResult(true);
            (int serverId, int optimisticId) = await validateTcs.Task.ConfigureAwait(false);
            Assert.That(serverId, Is.EqualTo(42));
            Assert.That(optimisticId, Is.EqualTo(1));
        }

        [Test]
        [Timeout(10000)]
        public async Task OptimisticPath_WhenValidateThrows_ErrorHandlerReceivesExceptionAndOptimistic()
        {
            CloudCodeSettings settings = ScriptableObject.CreateInstance<CloudCodeSettings>();
            var errorSignal = new TaskCompletionSource<(Exception Ex, object Optimistic)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var recordingErrors = new RecordingErrorHandler(errorSignal);

            var handler = new TestOptimisticHandler
            {
                ExpectedModule = Module,
                ExpectedEndpoint = Endpoint,
                OptimisticValue = new TestResponse { Id = 7 },
                ValidateCallback = (_, _) => throw new InvalidOperationException("validate failed"),
            };

            var registry = new CloudCodeOptimisticHandlerRegistry();
            registry.Register(handler);

            var fakeSdk = new FakeUnityCloudCodeService((_, _, _) => Task.FromResult("{\"id\":99}"));

            CloudCodeService service = new CloudCodeService(
                settings,
                new CloudCodeSdkCallHandler(fakeSdk),
                registry,
                recordingErrors);

            TestResponse returned = await service.CallEndpointAsync<TestResponse>(Module, Endpoint, new TestRequest()).ConfigureAwait(false);
            Assert.That(returned.Id, Is.EqualTo(7));

            (Exception ex, object optimistic) = await errorSignal.Task.ConfigureAwait(false);
            Assert.That(ex, Is.TypeOf<InvalidOperationException>());
            Assert.That(((TestResponse)optimistic).Id, Is.EqualTo(7));
        }

        [Test]
        public async Task WithoutOptimisticPath_NullPayload_AwaitsServerResponse()
        {
            CloudCodeSettings settings = ScriptableObject.CreateInstance<CloudCodeSettings>();
            var registry = new CloudCodeOptimisticHandlerRegistry();

            var fakeSdk = new FakeUnityCloudCodeService((_, _, _) => Task.FromResult("{\"id\":55}"));

            CloudCodeService service = new CloudCodeService(
                settings,
                new CloudCodeSdkCallHandler(fakeSdk),
                registry,
                new CloudCodeErrorHandler());

            TestResponse returned = await service.CallEndpointAsync<TestResponse>(Module, Endpoint, null).ConfigureAwait(false);
            Assert.That(returned.Id, Is.EqualTo(55));
        }

        [Test]
        public void TryResolve_FindsHandlerFromDiContainer_WhenNotInDictionary()
        {
            var handler = new TestOptimisticHandler
            {
                ExpectedModule = Module,
                ExpectedEndpoint = Endpoint,
                OptimisticValue = new TestResponse { Id = 42 },
            };

            var builder = new ContainerBuilder();
            builder.Register<CloudCodeOptimisticHandlerRegistry>(Lifetime.Singleton);
            builder.RegisterInstance(handler).As<IOptimisticCloudCodeHandler>().AsImplementedInterfaces();
            IObjectResolver container = builder.Build();
            CloudCodeOptimisticHandlerRegistry registry = container.Resolve<CloudCodeOptimisticHandlerRegistry>();

            bool ok = registry.TryResolve(Module, Endpoint, new TestRequest(), out IRequestHandler<TestResponse> resolved, out TestResponse optimistic);
            Assert.That(ok, Is.True);
            Assert.That(optimistic.Id, Is.EqualTo(42));
            Assert.That(resolved, Is.SameAs(handler));
        }

        [Test]
        public async Task WithoutOptimisticPath_TryMatchFails_AwaitsServerResponse()
        {
            CloudCodeSettings settings = ScriptableObject.CreateInstance<CloudCodeSettings>();

            var handler = new TestOptimisticHandler
            {
                ExpectedModule = "otherModule",
                ExpectedEndpoint = Endpoint,
                OptimisticValue = new TestResponse { Id = 1 },
            };

            var registry = new CloudCodeOptimisticHandlerRegistry();
            registry.Register(handler);

            var fakeSdk = new FakeUnityCloudCodeService((_, _, _) => Task.FromResult("{\"id\":88}"));

            CloudCodeService service = new CloudCodeService(
                settings,
                new CloudCodeSdkCallHandler(fakeSdk),
                registry,
                new CloudCodeErrorHandler());

            TestResponse returned = await service.CallEndpointAsync<TestResponse>(Module, Endpoint, new TestRequest()).ConfigureAwait(false);
            Assert.That(returned.Id, Is.EqualTo(88));
        }

        private sealed class TestRequest
        {
            public string Marker { get; set; }
        }

        private sealed class TestResponse
        {
            public int Id { get; set; }
        }

        private sealed class TestOptimisticHandler : IRequestHandler<TestRequest, TestResponse>, IOptimisticCloudCodeHandler
        {
            public Type RequestClrType => typeof(TestRequest);

            public Type ResponseClrType => typeof(TestResponse);

            public string ExpectedModule { get; init; }
            public string ExpectedEndpoint { get; init; }
            public TestResponse OptimisticValue { get; init; }
            public Action<TestResponse, TestResponse> ValidateCallback { get; init; }

            public bool TryMatch(string module, string endpoint, TestRequest request)
            {
                return module == ExpectedModule && endpoint == ExpectedEndpoint;
            }

            public TestResponse GetOptimisticResponse(TestRequest request)
            {
                return OptimisticValue;
            }

            public void Validate(TestResponse serverResponse, TestResponse optimisticResponse)
            {
                ValidateCallback?.Invoke(serverResponse, optimisticResponse);
            }
        }

        private sealed class RecordingErrorHandler : CloudCodeErrorHandler
        {
            private readonly TaskCompletionSource<(Exception Ex, object Optimistic)> signal;

            public RecordingErrorHandler(TaskCompletionSource<(Exception Ex, object Optimistic)> signal)
            {
                this.signal = signal;
            }

            public override void Handle(Exception exception, string module, string endpoint, object requestPayload, object optimisticResponseOrNull)
            {
                signal.TrySetResult((exception, optimisticResponseOrNull));
            }
        }

        private sealed class FakeUnityCloudCodeService : Unity.Services.CloudCode.ICloudCodeService
        {
            private readonly Func<string, string, Dictionary<string, object>, Task<string>> onCallModule;

            public FakeUnityCloudCodeService(Func<string, string, Dictionary<string, object>, Task<string>> onCallModule)
            {
                this.onCallModule = onCallModule;
            }

            public Task<string> CallModuleEndpointAsync(string module, string function, Dictionary<string, object> args = null)
            {
                return onCallModule(module, function, args);
            }

            public Task<string> CallEndpointAsync(string function, Dictionary<string, object> args = null) => NotSupported();

            public Task<TResult> CallEndpointAsync<TResult>(string function, Dictionary<string, object> args = null) => NotSupported<TResult>();

            public Task<TResult> CallModuleEndpointAsync<TResult>(string module, string function, Dictionary<string, object> args = null) => NotSupported<TResult>();

            public Task<ISubscriptionEvents> SubscribeToPlayerMessagesAsync(SubscriptionEventCallbacks callbacks) => NotSupported<ISubscriptionEvents>();

            public Task<ISubscriptionEvents> SubscribeToProjectMessagesAsync(SubscriptionEventCallbacks callbacks) => NotSupported<ISubscriptionEvents>();

            private static Task<string> NotSupported() => throw new NotSupportedException();

            private static Task<TResult> NotSupported<TResult>() => throw new NotSupportedException();
        }
    }
}
