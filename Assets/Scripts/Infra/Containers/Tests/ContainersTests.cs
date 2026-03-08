using System;
using NUnit.Framework;
using UnityEngine;

namespace Scaffold.Containers.Tests
{
    public class ContainersTests
    {
        [Test]
        public void Container_Build_CanBeOverridden_CallsCustomBuildMethod()
        {
            TestContainer container = new TestContainer();
            NullRegistry registry = new NullRegistry();
            container.Build(registry, null);
            Assert.IsTrue(container.WasBuildCalled);
        }

        [Test]
        public void Installer_Install_CanBeOverridden_CallsCustomInstallMethod()
        {
            TestInstaller installer = new TestInstaller();
            NullRegistry registry = new NullRegistry();
            installer.Install(registry, null);
            Assert.IsTrue(installer.WasInstallCalled);
        }

        [Test]
        public void ContainerLifetime_DefinesExpectedValues()
        {
            var lt = typeof(ContainerLifetime);
            bool hasSingleton = Enum.IsDefined(lt, "Singleton");
            bool hasScoped = Enum.IsDefined(lt, "Scoped");
            bool hasTransient = Enum.IsDefined(lt, "Transient");
            Assert.IsTrue(hasSingleton);
            Assert.IsTrue(hasScoped);
            Assert.IsTrue(hasTransient);
        }

        private class TestContainer : Container
        {
            public bool WasBuildCalled;

            public override void Build(IContainerRegistry registry, Transform holder)
            {
                WasBuildCalled = true;
            }
        }

        private class TestInstaller : Installer
        {
            public bool WasInstallCalled;

            public override void Install(IContainerRegistry registry, Transform holder)
            {
                WasInstallCalled = true;
            }
        }

        private class NullRegistry : IContainerRegistry
        {
            public IRegistrationBuilder<T> Register<T>(ContainerLifetime lifetime)
            {
                var builder = new NullBuilder<T>();
                return builder;
            }

            public IRegistrationBuilder<TService> Register<TService, TImpl>(ContainerLifetime lifetime) where TImpl : TService
            {
                var builder = new NullBuilder<TService>();
                return builder;
            }

            public IRegistrationBuilder<T> Register<T>(Func<IContainerResolver, T> factory, ContainerLifetime lifetime)
            {
                var builder = new NullBuilder<T>();
                return builder;
            }

            public IRegistrationBuilder<TEntryPoint> RegisterEntryPoint<TEntryPoint>(ContainerLifetime lifetime) where TEntryPoint : class
            {
                var builder = new NullBuilder<TEntryPoint>();
                return builder;
            }

            public void RegisterBuildCallback(Action<IContainerResolver> callback)
            {
            }
        }

        private class NullBuilder<T> : IRegistrationBuilder<T>
        {
            public IRegistrationBuilder<T> WithParameter<TParam>(TParam value)
            {
                return this;
            }

            public IRegistrationBuilder<T> AsImplementedInterfaces()
            {
                return this;
            }
        }
    }
}
