#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    internal static class VariablePayloadTypeHelpers
    {
        private static readonly Dictionary<Type, Type?> cacheByWrapperType = new();

        internal static bool TryResolvePayload(string payloadTypeId, string keyId, string context, out Type expected)
        {
            if (!VariableValueRegistry.TryResolve(payloadTypeId, out expected))
            {
                Debug.LogError($"{context}: unknown payload type id '{payloadTypeId}' for key '{keyId}'. Skipping rebase.");
                expected = default!;
                return false;
            }

            return true;
        }

        internal static Type? ExtractValueType(Type wrapperType)
        {
            if (wrapperType == null)
            {
                return null;
            }

            if (TryGetCachedValueType(wrapperType, out Type? hitOrNull))
            {
                return hitOrNull;
            }

            Type? found = FindInnerValueType(wrapperType);
            cacheByWrapperType[wrapperType] = found;
            return found;
        }

        private static bool TryGetCachedValueType(Type wrapperType, out Type? result)
        {
            if (cacheByWrapperType.TryGetValue(wrapperType, out Type? cached))
            {
                result = cached;
                return true;
            }

            result = null;
            return false;
        }

        private static Type? FindInnerValueType(Type wrapperType)
        {
            Type[] ifaces = wrapperType.GetInterfaces();
            for (int i = 0; i < ifaces.Length; i++)
            {
                Type iface = ifaces[i];
                if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != typeof(IVariableValue<>))
                {
                    continue;
                }

                Type[] args = iface.GetGenericArguments();
                if (args.Length == 1)
                {
                    return args[0];
                }
            }

            return null;
        }
    }
}
