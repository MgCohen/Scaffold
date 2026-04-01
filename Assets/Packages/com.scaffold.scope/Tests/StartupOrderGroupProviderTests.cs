using System;
using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.Scope;
using Scaffold.Scope.Contracts;
using VContainer;

namespace Scaffold.Scope.Tests
{
    /// <summary>
    /// Validates startup order grouping from inject-site edges among <see cref="IStartupOrderParticipant"/> types.
    /// </summary>
    public sealed class StartupOrderGroupProviderTests
    {
        [Test]
        public void GetOrderedGroups_ResolvesDependencyChainIntoThreeLevels()
        {
            var builder = new ContainerBuilder();
            RegisterParticipantA(builder);
            RegisterParticipant<ParticipantB>(builder);
            RegisterParticipant<ParticipantC>(builder);
            builder.Register<StartupOrderGroupProvider>(Lifetime.Singleton).As<IStartupOrderGroupProvider>();

            IObjectResolver container = builder.Build();
            IStartupOrderGroupProvider provider = container.Resolve<IStartupOrderGroupProvider>();
            IReadOnlyList<IReadOnlyList<Type>> groups = provider.GetOrderedGroups(container);

            Assert.That(groups.Count, Is.EqualTo(3));
            Assert.That(groups[0], Is.EquivalentTo(new[] { typeof(ParticipantA) }));
            Assert.That(groups[1], Is.EquivalentTo(new[] { typeof(ParticipantB) }));
            Assert.That(groups[2], Is.EquivalentTo(new[] { typeof(ParticipantC) }));
        }

        [Test]
        public void GetOrderedGroups_TwoParallelChains_ProducesThreeLevelsWithMergedRoots()
        {
            var builder = new ContainerBuilder();
            RegisterParticipantA(builder);
            RegisterParticipant<ParticipantB>(builder);
            RegisterParticipant<ParticipantC>(builder);
            RegisterParticipantD(builder);
            RegisterParticipant<ParticipantE>(builder);
            builder.Register<StartupOrderGroupProvider>(Lifetime.Singleton).As<IStartupOrderGroupProvider>();

            IObjectResolver container = builder.Build();
            IStartupOrderGroupProvider provider = container.Resolve<IStartupOrderGroupProvider>();
            IReadOnlyList<IReadOnlyList<Type>> groups = provider.GetOrderedGroups(container);

            Assert.That(groups.Count, Is.EqualTo(3));
            Assert.That(groups[0], Is.EquivalentTo(new[] { typeof(ParticipantA), typeof(ParticipantD) }));
            Assert.That(groups[1], Is.EquivalentTo(new[] { typeof(ParticipantB), typeof(ParticipantE) }));
            Assert.That(groups[2], Is.EquivalentTo(new[] { typeof(ParticipantC) }));
        }

        [Test]
        public void GetOrderedGroups_WhenSomeDependenciesAreNotParticipants_SkipsEdgesAndFlattensLevels()
        {
            var builder = new ContainerBuilder();
            builder.Register<NonParticipantA>(Lifetime.Singleton).AsSelf().As<ISeedA>();
            RegisterParticipant<ParticipantB>(builder);
            RegisterParticipant<ParticipantC>(builder);
            builder.Register<NonParticipantD>(Lifetime.Singleton).AsSelf().As<ISeedD>();
            RegisterParticipant<ParticipantE>(builder);
            builder.Register<StartupOrderGroupProvider>(Lifetime.Singleton).As<IStartupOrderGroupProvider>();

            IObjectResolver container = builder.Build();
            IStartupOrderGroupProvider provider = container.Resolve<IStartupOrderGroupProvider>();
            IReadOnlyList<IReadOnlyList<Type>> groups = provider.GetOrderedGroups(container);

            Assert.That(groups.Count, Is.EqualTo(2));
            Assert.That(groups[0], Is.EquivalentTo(new[] { typeof(ParticipantB), typeof(ParticipantE) }));
            Assert.That(groups[1], Is.EquivalentTo(new[] { typeof(ParticipantC) }));
        }

        private static void RegisterParticipant<T>(ContainerBuilder builder) where T : class, IStartupOrderParticipant
        {
            builder.Register<T>(Lifetime.Singleton).AsSelf().As<IStartupOrderParticipant>();
        }

        private static void RegisterParticipantA(ContainerBuilder builder)
        {
            builder.Register<ParticipantA>(Lifetime.Singleton).AsSelf().As<ISeedA>().As<IStartupOrderParticipant>();
        }

        private static void RegisterParticipantD(ContainerBuilder builder)
        {
            builder.Register<ParticipantD>(Lifetime.Singleton).AsSelf().As<ISeedD>().As<IStartupOrderParticipant>();
        }

        private interface ISeedA
        {
        }

        private interface ISeedD
        {
        }

        private sealed class ParticipantA : IStartupOrderParticipant, ISeedA
        {
        }

        private sealed class NonParticipantA : ISeedA
        {
        }

        private sealed class ParticipantB : IStartupOrderParticipant
        {
            public ParticipantB(ISeedA dependency)
            {
            }
        }

        private sealed class ParticipantC : IStartupOrderParticipant
        {
            public ParticipantC(ParticipantB dependency)
            {
            }
        }

        private sealed class ParticipantD : IStartupOrderParticipant, ISeedD
        {
        }

        private sealed class NonParticipantD : ISeedD
        {
        }

        private sealed class ParticipantE : IStartupOrderParticipant
        {
            public ParticipantE(ISeedD dependency)
            {
            }
        }
    }
}
