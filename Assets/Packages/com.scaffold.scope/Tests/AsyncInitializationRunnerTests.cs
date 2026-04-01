using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.Scope;
using Scaffold.Scope.Contracts;
using VContainer;

namespace Scaffold.Scope.Tests
{
    /// <summary>
    /// Validates dependency-derived async init: <see cref="IAsyncInitializable.InitializeAsync"/> runs in parallel within a wave; waves run sequentially.
    /// </summary>
    public sealed class AsyncInitializationRunnerTests
    {
        [Test]
        public async Task RunAsync_Chain_StartsInitializeInTopologicalOrder()
        {
            var recorder = new InitRecorder();
            var builder = new ContainerBuilder();
            builder.RegisterInstance(recorder);
            builder.Register<InitA>(Lifetime.Singleton).AsSelf().As<ISeedA>().As<IAsyncInitializable>();
            builder.Register<InitB>(Lifetime.Singleton).AsSelf().As<IAsyncInitializable>();
            builder.Register<InitC>(Lifetime.Singleton).AsSelf().As<IAsyncInitializable>();
            builder.Register<AsyncInitializationRunner>(Lifetime.Singleton).As<IAsyncInitializationRunner>();

            IObjectResolver container = builder.Build();
            IAsyncInitializationRunner runner = container.Resolve<IAsyncInitializationRunner>();
            await runner.RunAsync(container, CancellationToken.None);

            Assert.That(recorder.StartOrder, Is.EqualTo(new[] { nameof(InitA), nameof(InitB), nameof(InitC) }));
        }

        [Test]
        public async Task RunAsync_TwoParallelChains_FirstWaveContainsBothRoots()
        {
            var recorder = new InitRecorder();
            IObjectResolver container = BuildParallelChainContainer(recorder);
            IAsyncInitializationRunner runner = container.Resolve<IAsyncInitializationRunner>();
            await runner.RunAsync(container, CancellationToken.None);
            VerifyParallelWaveOrder(recorder.StartOrder);
        }

        [Test]
        public void RunAsync_Cycle_Throws()
        {
            try
            {
                var builder = new ContainerBuilder();
                builder.Register<InitCycleA>(Lifetime.Singleton).AsSelf().As<IAsyncInitializable>();
                builder.Register<InitCycleB>(Lifetime.Singleton).AsSelf().As<IAsyncInitializable>();
                builder.Register<AsyncInitializationRunner>(Lifetime.Singleton).As<IAsyncInitializationRunner>();

                IObjectResolver container = builder.Build();
                _ = container.Resolve<IAsyncInitializationRunner>();
                // Constructor cycles surface when resolving IAsyncInitializable (same path as RunAsync).
                container.Resolve<IEnumerable<IAsyncInitializable>>().ToList();
            }
            catch (Exception ex)
            {
                Assert.That(ex.Message, Does.Contain("Circular dependency detected"));
                return;
            }

            Assert.Fail("Expected circular dependency when resolving IAsyncInitializable.");
        }

        private void VerifyParallelWaveOrder(IReadOnlyList<string> order)
        {
            Assert.That(order.Count, Is.EqualTo(5));
            IEnumerable<string> firstPair = order.Take(2);
            var firstWave = new HashSet<string>(firstPair);
            IEnumerable<string> secondPair = order.Skip(2).Take(2);
            var secondWave = new HashSet<string>(secondPair);
            string[] expectedFirst = new[] { nameof(InitA), nameof(InitD) };
            string[] expectedSecond = new[] { nameof(InitB), nameof(InitE) };
            Assert.That(firstWave, Is.EquivalentTo(expectedFirst));
            Assert.That(secondWave, Is.EquivalentTo(expectedSecond));
            Assert.That(order[4], Is.EqualTo(nameof(InitC)));
        }

        private static IObjectResolver BuildParallelChainContainer(InitRecorder recorder)
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(recorder);
            builder.Register<InitA>(Lifetime.Singleton).AsSelf().As<ISeedA>().As<IAsyncInitializable>();
            builder.Register<InitB>(Lifetime.Singleton).AsSelf().As<IAsyncInitializable>();
            builder.Register<InitC>(Lifetime.Singleton).AsSelf().As<IAsyncInitializable>();
            builder.Register<InitD>(Lifetime.Singleton).AsSelf().As<ISeedD>().As<IAsyncInitializable>();
            builder.Register<InitE>(Lifetime.Singleton).AsSelf().As<IAsyncInitializable>();
            builder.Register<AsyncInitializationRunner>(Lifetime.Singleton).As<IAsyncInitializationRunner>();
            return builder.Build();
        }

        private interface ISeedA
        {
        }

        private interface ISeedD
        {
        }

        private sealed class InitRecorder
        {
            public IReadOnlyList<string> StartOrder
            {
                get
                {
                    lock (sync)
                    {
                        return starts.ToArray();
                    }
                }
            }

            private readonly object sync = new object();
            private readonly List<string> starts = new List<string>();

            public void RecordStart(string name)
            {
                lock (sync)
                {
                    starts.Add(name);
                }
            }
        }

        private sealed class InitA : IAsyncInitializable, ISeedA
        {
            public InitA(InitRecorder recorder)
            {
                this.recorder = recorder;
            }

            private readonly InitRecorder recorder;

            public Task InitializeAsync(CancellationToken cancellationToken)
            {
                recorder.RecordStart(nameof(InitA));
                return Task.CompletedTask;
            }
        }

        private sealed class InitB : IAsyncInitializable
        {
            public InitB(ISeedA seed, InitRecorder recorder)
            {
                this.recorder = recorder;
            }

            private readonly InitRecorder recorder;

            public Task InitializeAsync(CancellationToken cancellationToken)
            {
                recorder.RecordStart(nameof(InitB));
                return Task.CompletedTask;
            }
        }

        private sealed class InitC : IAsyncInitializable
        {
            public InitC(InitB dependency, InitRecorder recorder)
            {
                this.recorder = recorder;
            }

            private readonly InitRecorder recorder;

            public Task InitializeAsync(CancellationToken cancellationToken)
            {
                recorder.RecordStart(nameof(InitC));
                return Task.CompletedTask;
            }
        }

        private sealed class InitD : IAsyncInitializable, ISeedD
        {
            public InitD(InitRecorder recorder)
            {
                this.recorder = recorder;
            }

            private readonly InitRecorder recorder;

            public Task InitializeAsync(CancellationToken cancellationToken)
            {
                recorder.RecordStart(nameof(InitD));
                return Task.CompletedTask;
            }
        }

        private sealed class InitE : IAsyncInitializable
        {
            public InitE(ISeedD seed, InitRecorder recorder)
            {
                this.recorder = recorder;
            }

            private readonly InitRecorder recorder;

            public Task InitializeAsync(CancellationToken cancellationToken)
            {
                recorder.RecordStart(nameof(InitE));
                return Task.CompletedTask;
            }
        }

        private sealed class InitCycleA : IAsyncInitializable
        {
            public InitCycleA(InitCycleB b)
            {
            }

            public Task InitializeAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class InitCycleB : IAsyncInitializable
        {
            public InitCycleB(InitCycleA a)
            {
            }

            public Task InitializeAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
