using Scaffold.Containers;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Events.Container
{
    public class EventsInstaller : Installer
    {
        public override void Install(IContainerRegistry registry, Transform holder)
        {
            RegisterDiagnosticsSink(registry);
            RegisterScalableRuntime(registry);
            RegisterBusContracts(registry);
        }

        private static void RegisterDiagnosticsSink(IContainerRegistry registry)
        {
            registry.Register<IEventDiagnosticsSink>(_ => NoOpEventDiagnosticsSink.Instance, ContainerLifetime.Scoped);
        }

        private static void RegisterScalableRuntime(IContainerRegistry registry)
        {
            registry.Register<ScalableEventBus>(CreateScalableRuntime, ContainerLifetime.Scoped);
        }

        private static void RegisterBusContracts(IContainerRegistry registry)
        {
            registry.Register<IEventBus>(ResolveScalableRuntime, ContainerLifetime.Scoped);
            registry.Register<IRequestBus>(ResolveScalableRuntime, ContainerLifetime.Scoped);
        }

        private static ScalableEventBus CreateScalableRuntime(IContainerResolver resolver)
        {
            IEnumerable<IEventMiddleware> eventMiddlewares = ResolveMany<IEventMiddleware>(resolver);
            IEnumerable<IRequestMiddleware> requestMiddlewares = ResolveMany<IRequestMiddleware>(resolver);
            IEventDiagnosticsSink diagnosticsSink = resolver.Resolve<IEventDiagnosticsSink>();
            return new ScalableEventBus(eventMiddlewares, requestMiddlewares, diagnosticsSink);
        }

        private static ScalableEventBus ResolveScalableRuntime(IContainerResolver resolver)
        {
            return resolver.Resolve<ScalableEventBus>();
        }

        private static IEnumerable<TService> ResolveMany<TService>(IContainerResolver resolver)
        {
            try
            {
                return resolver.Resolve<IEnumerable<TService>>();
            }
            catch (Exception)
            {
                return Array.Empty<TService>();
            }
        }
    }
}
