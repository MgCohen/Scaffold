#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LiveOps.DTO.Keys
{
    public static class LiveOpsKeyResolver
    {
        private static readonly ConcurrentDictionary<RuntimeTypeHandle, LiveOpsKeyResolution> s_map = new();

        /// <summary>
        /// Called once per DTO assembly from the source-generated <c>[ModuleInitializer]</c> in
        /// <c>LiveOpsKeyRuntimeMap</c>.
        /// </summary>
        public static void Contribute(ReadOnlySpan<KeyValuePair<RuntimeTypeHandle, LiveOpsKeyResolution>> entries)
        {
            foreach (KeyValuePair<RuntimeTypeHandle, LiveOpsKeyResolution> kv in entries)
            {
                s_map.TryAdd(kv.Key, kv.Value);
            }
        }

        /// <summary>
        /// Full resolution for <see cref="KeyOf{T}"/> (single map lookup).
        /// </summary>
        internal static LiveOpsKeyResolution GetResolution(Type t) => Lookup(t);

        public static string GetModuleKey(Type t) => Lookup(t).Module;

        public static string GetWireKey(Type t)
        {
            string? wire = Lookup(t).Wire;
            if (wire is null)
            {
                throw new InvalidOperationException(
                    $"No wire key for {t.FullName}. Wire keys apply only to types that inherit " +
                    "ModuleRequest or carry [GameApiRequest] with a non-empty wire key.");
            }

            return wire;
        }

        private static LiveOpsKeyResolution Lookup(Type t)
        {
            if (t is null)
            {
                throw new ArgumentNullException(nameof(t));
            }

            if (s_map.TryGetValue(t.TypeHandle, out LiveOpsKeyResolution r))
            {
                return r;
            }

            throw new InvalidOperationException(
                $"No LiveOps key registered for {t.FullName}. " +
                "Ensure the declaring assembly references Scaffold.LiveOps.Bootstrap.Generators " +
                "(OutputItemType=\"Analyzer\" ReferenceOutputAssembly=\"false\").");
        }
    }
}
