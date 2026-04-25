#nullable enable
using System;

namespace LiveOps.DTO.Keys
{
    public static class KeyOf
    {
        public static string WireOf(object instance) =>
            instance is null
                ? throw new ArgumentNullException(nameof(instance))
                : WireOf(instance.GetType());

        public static string WireOf(Type t) => LiveOpsKeyResolver.GetWireKey(t);
    }

    public static class KeyOf<T>
    {
        private static readonly LiveOpsKeyResolution s_resolution = LiveOpsKeyResolver.GetResolution(typeof(T));

        public static string Module => s_resolution.Module;

        /// <summary>
        /// GameApi wire key when applicable; otherwise null (for example persistence/config DTOs).
        /// </summary>
        public static string? Wire => s_resolution.Wire;
    }
}
