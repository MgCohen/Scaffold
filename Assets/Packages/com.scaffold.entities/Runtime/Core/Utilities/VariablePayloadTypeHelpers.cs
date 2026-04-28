using System;
using UnityEngine;

namespace Scaffold.Entities
{
    internal static class VariablePayloadTypeHelpers
    {
        internal static bool TryResolvePayload(Variable k, string context, out Type expected)
        {
            if (!VariableValueRegistry.TryResolve(k.PayloadTypeId, out expected))
            {
                Debug.LogError($"{context}: unknown payload type id '{k.PayloadTypeId}' for key '{k.Key}'. Skipping rebase.");
                expected = default!;
                return false;
            }

            return true;
        }
    }
}
