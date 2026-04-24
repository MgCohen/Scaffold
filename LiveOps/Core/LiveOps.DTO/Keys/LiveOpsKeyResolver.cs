using System;
using System.Reflection;

namespace LiveOps.DTO.Keys
{
    public static class LiveOpsKeyResolver
    {
        public static string GetModuleKey(Type t)
        {
            if (t is null) throw new ArgumentNullException(nameof(t));
            LiveOpsKeyAttribute attr = t.GetTypeInfo().GetCustomAttribute<LiveOpsKeyAttribute>();
            if (attr != null)
            {
                return attr.Value;
            }

            return t.Name;
        }

        public static string GetWireKey(Type t)
        {
            if (t is null) throw new ArgumentNullException(nameof(t));
            GameApiRequestAttribute api = t.GetTypeInfo().GetCustomAttribute<GameApiRequestAttribute>();
            if (api != null && !string.IsNullOrEmpty(api.WireKey))
            {
                return api.WireKey;
            }

            Type moduleRequestBase = typeof(LiveOps.DTO.ModuleRequest.ModuleRequest);
            if (moduleRequestBase.IsAssignableFrom(t))
            {
                return t.Name;
            }

            return GetModuleKey(t);
        }
    }
}
