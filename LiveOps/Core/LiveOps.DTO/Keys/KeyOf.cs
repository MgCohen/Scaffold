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
        public static readonly string Module = LiveOpsKeyResolver.GetModuleKey(typeof(T));

        public static readonly string Wire = LiveOpsKeyResolver.GetWireKey(typeof(T));
    }
}
