using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LiveOps.DTO.GameApi;
using LiveOps.DTO.ModuleRequest;
using NUnit.Framework;
using Scaffold.AppFlow;
using Scaffold.CloudCode;
using Scaffold.LiveOps.Container;
using VContainer;

namespace Scaffold.LiveOps.Tests
{
    public sealed class LiveOpsServiceOptimisticTests
    {
        private const string LiveOpsModule = "LiveOps";

        [Test]
        [Timeout(10000)]
        public async Task GameApiOptimistic_ReturnsImmediately_ThenValidateRunsWhenEnvelopeCompletes()
        {
            var serverGate = new TaskCompletionSource<GameApiEnvelopeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            var validateTcs = new TaskCompletionSource<(int ServerId, int OptimisticId)>(TaskCreationOptions.RunContinuationsAsynchronously);

            var handler = new TestOptimisticHandler
            {
                ExpectedModule = LiveOpsModule,
                ExpectedEndpoint = "GameApi",
                OptimisticValue = new OptimisticGameApiResponse { Id = 1 },
                ValidateCallback = (server, optimistic) => validateTcs.TrySetResult((server.Id, optimistic.Id)),
            };

            var fakeCloud = new FakeCloudCodeService(() => serverGate.Task);

            ILiveOpsService sut = BuildSut(fakeCloud, new CloudCodeErrorHandler(), handler);

            Task<OptimisticGameApiResponse> call = sut.CallAsync(new OptimisticGameApiRequest { Marker = "x" }, CancellationToken.None);
            OptimisticGameApiResponse returned = await call.ConfigureAwait(false);
            Assert.That(returned.Id, Is.EqualTo(1));
            Assert.That(validateTcs.Task.IsCompleted, Is.False, "validate must not run until the envelope task completes");

            serverGate.TrySetResult(GameApiEnvelopeResponse.Success(nameof(OptimisticGameApiRequest), new OptimisticGameApiResponse { Id = 42 }, null));
            (int serverId, int optimisticId) = await validateTcs.Task.ConfigureAwait(false);
            Assert.That(serverId, Is.EqualTo(42));
            Assert.That(optimisticId, Is.EqualTo(1));
        }

        [Test]
        [Timeout(10000)]
        public async Task GameApiOptimistic_WhenEnvelopeIsException_ErrorHandlerReceivesExceptionAndOptimistic()
        {
            var errorSignal = new TaskCompletionSource<(Exception Ex, object Optimistic)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var recordingErrors = new RecordingErrorHandler(errorSignal);

            var handler = new TestOptimisticHandler
            {
                ExpectedModule = LiveOpsModule,
                ExpectedEndpoint = "GameApi",
                OptimisticValue = new OptimisticGameApiResponse { Id = 7 },
            };

            var fakeCloud = new FakeCloudCodeService(
                () => Task.FromResult(GameApiEnvelopeResponse.Exception(nameof(OptimisticGameApiRequest), new InvalidOperationException("server failed"))));

            ILiveOpsService sut = BuildSut(fakeCloud, recordingErrors, handler);

            OptimisticGameApiResponse returned = await sut.CallAsync(new OptimisticGameApiRequest(), CancellationToken.None).ConfigureAwait(false);
            Assert.That(returned.Id, Is.EqualTo(7));

            (Exception ex, object optimistic) = await errorSignal.Task.ConfigureAwait(false);
            Assert.That(ex, Is.TypeOf<InvalidOperationException>());
            Assert.That(ex.Message, Does.Contain("server failed"));
            Assert.That(((OptimisticGameApiResponse)optimistic).Id, Is.EqualTo(7));
        }

        [Test]
        [Timeout(10000)]
        public async Task GameApiOptimistic_WhenValidateThrows_ErrorHandlerReceivesExceptionAndOptimistic()
        {
            var errorSignal = new TaskCompletionSource<(Exception Ex, object Optimistic)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var recordingErrors = new RecordingErrorHandler(errorSignal);

            var handler = new TestOptimisticHandler
            {
                ExpectedModule = LiveOpsModule,
                ExpectedEndpoint = "GameApi",
                OptimisticValue = new OptimisticGameApiResponse { Id = 11 },
                ValidateCallback = (_, _) => throw new InvalidOperationException("validate failed"),
            };

            var fakeCloud = new FakeCloudCodeService(
                () => Task.FromResult(GameApiEnvelopeResponse.Success(nameof(OptimisticGameApiRequest), new OptimisticGameApiResponse { Id = 99 }, null)));

            ILiveOpsService sut = BuildSut(fakeCloud, recordingErrors, handler);

            OptimisticGameApiResponse returned = await sut.CallAsync(new OptimisticGameApiRequest(), CancellationToken.None).ConfigureAwait(false);
            Assert.That(returned.Id, Is.EqualTo(11));

            (Exception ex, object optimistic) = await errorSignal.Task.ConfigureAwait(false);
            Assert.That(ex, Is.TypeOf<InvalidOperationException>());
            Assert.That(ex.Message, Does.Contain("validate failed"));
            Assert.That(((OptimisticGameApiResponse)optimistic).Id, Is.EqualTo(11));
        }

        [Test]
        [Timeout(10000)]
        public async Task GameApiOptimistic_NestedResponsesDispatchOnce_AfterReconciliation()
        {
            var serverGate = new TaskCompletionSource<GameApiEnvelopeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            var validateTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var nestedHandler = new RecordingNestedHandler();
            var handler = new TestOptimisticHandler
            {
                ExpectedModule = LiveOpsModule,
                ExpectedEndpoint = "GameApi",
                OptimisticValue = new OptimisticGameApiResponse { Id = 1 },
                ValidateCallback = (_, _) => validateTcs.TrySetResult(true),
            };

            var fakeCloud = new FakeCloudCodeService(() => serverGate.Task);

            ILiveOpsService sut = BuildSut(fakeCloud, new CloudCodeErrorHandler(), handler, nestedHandler);

            Task<OptimisticGameApiResponse> call = sut.CallAsync(new OptimisticGameApiRequest(), CancellationToken.None);
            _ = await call.ConfigureAwait(false);
            Assert.That(nestedHandler.CallCount, Is.EqualTo(0), "nested handlers must not run before reconciliation");

            var nested = new List<ModuleResponse> { new NestedAuditResponse { Hits = 1 } };
            serverGate.TrySetResult(GameApiEnvelopeResponse.Success(nameof(OptimisticGameApiRequest), new OptimisticGameApiResponse { Id = 2 }, nested));

            await validateTcs.Task.ConfigureAwait(false);
            Assert.That(nestedHandler.CallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task GameApi_WithoutRegisteredHandler_AwaitsEnvelopeAndReturnsServerResult()
        {
            var fakeCloud = new FakeCloudCodeService(
                () => Task.FromResult(GameApiEnvelopeResponse.Success(nameof(OptimisticGameApiRequest), new OptimisticGameApiResponse { Id = 55 }, null)));

            ILiveOpsService sut = BuildSut(fakeCloud, new CloudCodeErrorHandler(), optimisticHandler: null);

            OptimisticGameApiResponse returned = await sut.CallAsync(new OptimisticGameApiRequest(), CancellationToken.None).ConfigureAwait(false);
            Assert.That(returned.Id, Is.EqualTo(55));
        }

        [Test]
        public async Task GameApiOptimistic_ExplicitRegisterOverridesContainerDiscovery()
        {
            var fakeCloud = new FakeCloudCodeService(
                () => Task.FromResult(GameApiEnvelopeResponse.Success(nameof(OptimisticGameApiRequest), new OptimisticGameApiResponse { Id = 42 }, null)));

            var containerHandler = new TestOptimisticHandler
            {
                ExpectedModule = LiveOpsModule,
                ExpectedEndpoint = "GameApi",
                OptimisticValue = new OptimisticGameApiResponse { Id = 1 },
            };

            var explicitHandler = new TestOptimisticHandler
            {
                ExpectedModule = LiveOpsModule,
                ExpectedEndpoint = "GameApi",
                OptimisticValue = new OptimisticGameApiResponse { Id = 999 },
            };

            ILiveOpsService sut = BuildSutWithExplicitRegistryOverride(fakeCloud, new CloudCodeErrorHandler(), containerHandler, explicitHandler);

            OptimisticGameApiResponse returned = await sut.CallAsync(new OptimisticGameApiRequest(), CancellationToken.None).ConfigureAwait(false);
            Assert.That(returned.Id, Is.EqualTo(999));
        }

        private static ILiveOpsService BuildSut(
            ICloudCodeService cloudCode,
            CloudCodeErrorHandler errorHandler,
            IOptimisticCloudCodeHandler optimisticHandler,
            params IResponseHandler[] nestedHandlers)
        {
            var builder = new ContainerBuilder();
            builder.Register<ContainerLayerResolver>(Lifetime.Singleton).As<ILayerResolver>();
            builder.RegisterInstance(cloudCode).As<ICloudCodeService>();
            builder.Register<CloudCodeOptimisticHandlerRegistry>(Lifetime.Singleton);
            builder.RegisterInstance(errorHandler);
            builder.RegisterInstance(NoMatchResponseHandler.Instance).As<IResponseHandler>();
            if (optimisticHandler != null)
            {
                builder.RegisterInstance(optimisticHandler).As<IOptimisticCloudCodeHandler>().AsImplementedInterfaces();
                if (optimisticHandler is TestOptimisticHandler testOptimistic)
                {
                    builder.RegisterBuildCallback(resolver =>
                    {
                        CloudCodeOptimisticHandlerRegistry registry = resolver.Resolve<CloudCodeOptimisticHandlerRegistry>();
                        registry.Register(testOptimistic);
                    });
                }
            }

            foreach (IResponseHandler h in nestedHandlers)
            {
                if (h != null)
                {
                    builder.RegisterInstance(h).As<IResponseHandler>();
                }
            }

            new LiveOpsInstaller().Install(builder);
            IObjectResolver container = builder.Build();
            return container.Resolve<ILiveOpsService>();
        }

        private static ILiveOpsService BuildSutWithExplicitRegistryOverride(
            ICloudCodeService cloudCode,
            CloudCodeErrorHandler errorHandler,
            IOptimisticCloudCodeHandler containerHandler,
            IRequestHandler<OptimisticGameApiRequest, OptimisticGameApiResponse> explicitHandler)
        {
            var builder = new ContainerBuilder();
            builder.Register<ContainerLayerResolver>(Lifetime.Singleton).As<ILayerResolver>();
            builder.RegisterInstance(cloudCode).As<ICloudCodeService>();
            builder.Register<CloudCodeOptimisticHandlerRegistry>(Lifetime.Singleton);
            builder.RegisterInstance(errorHandler);
            builder.RegisterInstance(NoMatchResponseHandler.Instance).As<IResponseHandler>();
            builder.RegisterInstance(containerHandler).As<IOptimisticCloudCodeHandler>().AsImplementedInterfaces();
            builder.RegisterBuildCallback(resolver =>
            {
                CloudCodeOptimisticHandlerRegistry registry = resolver.Resolve<CloudCodeOptimisticHandlerRegistry>();
                registry.Register(explicitHandler);
            });
            new LiveOpsInstaller().Install(builder);
            return builder.Build().Resolve<ILiveOpsService>();
        }

        private sealed class OptimisticGameApiRequest : ModuleRequest<OptimisticGameApiResponse>
        {
            public string Marker { get; set; }
        }

        private sealed class OptimisticGameApiResponse : ModuleResponse
        {
            public int Id { get; set; }
        }

        private sealed class NestedAuditResponse : ModuleResponse
        {
            public int Hits { get; set; }
        }

        private sealed class TestOptimisticHandler : IRequestHandler<OptimisticGameApiRequest, OptimisticGameApiResponse>, IOptimisticCloudCodeHandler
        {
            public Type RequestClrType => typeof(OptimisticGameApiRequest);

            public Type ResponseClrType => typeof(OptimisticGameApiResponse);

            public string ExpectedModule { get; init; }
            public string ExpectedEndpoint { get; init; }
            public OptimisticGameApiResponse OptimisticValue { get; init; }
            public Action<OptimisticGameApiResponse, OptimisticGameApiResponse> ValidateCallback { get; init; }

            public bool TryMatch(string module, string endpoint, OptimisticGameApiRequest request)
            {
                return module == ExpectedModule && endpoint == ExpectedEndpoint;
            }

            public OptimisticGameApiResponse GetOptimisticResponse(OptimisticGameApiRequest request)
            {
                return OptimisticValue;
            }

            public void Validate(OptimisticGameApiResponse serverResponse, OptimisticGameApiResponse optimisticResponse)
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

        private sealed class RecordingNestedHandler : IResponseHandler
        {
            public int CallCount { get; private set; }

            public Type HandledResponseType => typeof(NestedAuditResponse);

            public void Handle(ModuleResponse response)
            {
                CallCount++;
            }
        }

        private sealed class NoMatchResponseStub : ModuleResponse
        {
        }

        private sealed class NoMatchResponseHandler : IResponseHandler
        {
            internal static readonly NoMatchResponseHandler Instance = new NoMatchResponseHandler();

            private NoMatchResponseHandler()
            {
            }

            public Type HandledResponseType => typeof(NoMatchResponseStub);

            public void Handle(ModuleResponse response)
            {
            }
        }

        private sealed class ContainerLayerResolver : ILayerResolver
        {
            public ContainerLayerResolver(IObjectResolver top)
            {
                Top = top ?? throw new ArgumentNullException(nameof(top));
            }

            public IObjectResolver Top { get; }

            public bool TryResolve<T>(out T value)
            {
                return Top.TryResolve(out value);
            }

            public T Resolve<T>()
            {
                return Top.Resolve<T>();
            }
        }

        private sealed class FakeCloudCodeService : ICloudCodeService
        {
            private readonly Func<Task<GameApiEnvelopeResponse>> onGameApi;

            public int GameApiCallCount { get; private set; }

            public FakeCloudCodeService(Func<Task<GameApiEnvelopeResponse>> onGameApi)
            {
                this.onGameApi = onGameApi;
            }

            public Task<T> CallEndpointAsync<T>(string module, string endpoint, object payload = null, CancellationToken cancellationToken = default)
            {
                if (endpoint == "GameApi")
                {
                    GameApiCallCount++;
                    return AwaitGameApiTyped<T>();
                }

                throw new InvalidOperationException($"Unexpected endpoint: {endpoint}.");
            }

            private async Task<T> AwaitGameApiTyped<T>()
            {
                GameApiEnvelopeResponse envelope = await onGameApi().ConfigureAwait(false);
                return (T)(object)envelope;
            }
        }
    }
}
