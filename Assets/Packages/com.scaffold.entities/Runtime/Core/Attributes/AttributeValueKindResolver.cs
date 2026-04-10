using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    internal static class AttributeValueKindResolver
    {
        private static readonly object Gate = new object();
        private static AttributeValueKindRegistrySO cachedRegistry;
        private static readonly Dictionary<AttributeValueType, AttributeDefinitionBase> LegacyFallback =
            new Dictionary<AttributeValueType, AttributeDefinitionBase>
            {
                { AttributeValueType.Float, new FloatScalarAttributeDefinition() },
                { AttributeValueType.Int, new IntScalarAttributeDefinition() },
                { AttributeValueType.Bool, new BoolScalarAttributeDefinition() },
                { AttributeValueType.String, new StringScalarAttributeDefinition() }
            };

        public static void SetGlobalRegistry(AttributeValueKindRegistrySO registry)
        {
            lock (Gate)
            {
                cachedRegistry = registry;
            }
        }

        internal static bool TryResolveDefinition(AttributeSO attribute, out AttributeDefinitionBase definition)
        {
            definition = null!;
            if (attribute == null)
            {
                return false;
            }

            AttributeValueKindRegistrySO registry = attribute.KindRegistryOverride ?? GetRegistry();
            if (registry != null)
            {
                string id = attribute.ValueKindId;
                if (!string.IsNullOrEmpty(id) && registry.TryGetByStableId(id, out definition))
                {
                    return true;
                }

                if (attribute.ValueType != AttributeValueType.Custom
                    && registry.TryGetFirstForLegacyType(attribute.ValueType, out definition))
                {
                    return true;
                }
            }

            if (attribute.ValueType != AttributeValueType.Custom
                && LegacyFallback.TryGetValue(attribute.ValueType, out definition))
            {
                return true;
            }

            return false;
        }

        private static AttributeValueKindRegistrySO GetRegistry()
        {
            lock (Gate)
            {
                if (cachedRegistry != null)
                {
                    return cachedRegistry;
                }
            }

            return Resources.Load<AttributeValueKindRegistrySO>("AttributeValueKindRegistry");
        }
    }
}
