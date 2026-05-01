using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LiveOps.DTO.Keys;

namespace LiveOps.Tests
{
    /// <summary>
    /// Registers <see cref="LiveOpsKeyResolution" /> for test-only DTOs (this assembly does not run the LiveOps source generator).
    /// </summary>
    internal static class LiveOpsTestKeysModuleInitializer
    {
        [ModuleInitializer]
        internal static void Register()
        {
            LiveOpsKeyResolver.Contribute(
                new KeyValuePair<RuntimeTypeHandle, LiveOpsKeyResolution>[]
                {
                    Entry<GameApiRegistryTests.M1ARequest>("M1ARequest", "M1ARequest"),
                    Entry<GameApiRegistryTests.NoKeyRequest>("NoKeyRequest", "NoKeyRequest"),
                    Entry<GameApiRegistryTests.CollideNameNs1.CollideRequest>("CollideRequest", "CollideRequest"),
                    Entry<GameApiRegistryTests.CollideNameNs2.CollideRequest>("CollideRequest", "CollideRequest"),
                    Entry<GameApiDispatcherTests.PingRequest>("PingRequest", "PingRequest"),
                    Entry<GameApiDispatcherTests.BoomRequest>("BoomRequest", "BoomRequest"),
                    Entry<ModulePrefetchKeysTests.StubData>("StubData", null),
                    Entry<RegisterModuleSingleInstanceTests.SampleData>("SampleData", null),
                });
        }

        private static KeyValuePair<RuntimeTypeHandle, LiveOpsKeyResolution> Entry<T>(
            string module,
            string? wire) =>
            new(
                typeof(T).TypeHandle,
                new LiveOpsKeyResolution(module, wire));
    }
}
