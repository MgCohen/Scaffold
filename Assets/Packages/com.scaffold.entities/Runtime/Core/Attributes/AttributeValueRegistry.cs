using System;
using System.Collections.Generic;

namespace Scaffold.Entities
{
    public static class AttributeValueRegistry
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<Type, Func<AttributeValue>> FactoriesByConcreteType =
            new Dictionary<Type, Func<AttributeValue>>();
        private static readonly Dictionary<AttributeValueType, Type> LegacyKindToConcreteType =
            new Dictionary<AttributeValueType, Type>();

        static AttributeValueRegistry()
        {
            RegisterLegacy(AttributeValueType.Float, () => new FloatAttributeValue());
            RegisterLegacy(AttributeValueType.Int, () => new IntAttributeValue());
            RegisterLegacy(AttributeValueType.Bool, () => new BoolAttributeValue());
            RegisterLegacy(AttributeValueType.String, () => new StringAttributeValue());
        }

        public static void Register(Type concreteAttributeValueType, Func<AttributeValue> createDefault)
        {
            if (concreteAttributeValueType == null)
            {
                throw new ArgumentNullException(nameof(concreteAttributeValueType));
            }

            if (createDefault == null)
            {
                throw new ArgumentNullException(nameof(createDefault));
            }

            if (!typeof(AttributeValue).IsAssignableFrom(concreteAttributeValueType))
            {
                throw new ArgumentException(
                    $"Type {concreteAttributeValueType.FullName} must derive from {nameof(AttributeValue)}.",
                    nameof(concreteAttributeValueType));
            }

            lock (Gate)
            {
                FactoriesByConcreteType[concreteAttributeValueType] = createDefault;
            }
        }

        public static void Register(IAttributeDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Register(definition.ValueType, definition.CreateDefault);
        }

        public static void Register<T>(IAttributeDefinition<T> definition)
            where T : AttributeValue
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Register(definition.ValueType, definition.CreateDefault);
        }

        public static void RegisterRange(IEnumerable<IAttributeDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            foreach (IAttributeDefinition definition in definitions)
            {
                Register(definition);
            }
        }

        public static bool TryCreate(Type concreteAttributeValueType, out AttributeValue value)
        {
            value = null!;
            if (concreteAttributeValueType == null)
            {
                return false;
            }

            lock (Gate)
            {
                if (FactoriesByConcreteType.TryGetValue(concreteAttributeValueType, out Func<AttributeValue> factory))
                {
                    value = factory();
                    return value != null;
                }
            }

            return false;
        }

        public static bool TryCreate(AttributeValueType legacyKind, out AttributeValue value)
        {
            value = null!;
            lock (Gate)
            {
                if (!LegacyKindToConcreteType.TryGetValue(legacyKind, out Type concrete))
                {
                    return false;
                }

                if (FactoriesByConcreteType.TryGetValue(concrete, out Func<AttributeValue> factory))
                {
                    value = factory();
                    return value != null;
                }
            }

            return false;
        }

        internal static bool TryGetConcreteType(AttributeValueType legacyKind, out Type concreteType)
        {
            lock (Gate)
            {
                return LegacyKindToConcreteType.TryGetValue(legacyKind, out concreteType!);
            }
        }

        internal static void Unregister(Type concreteAttributeValueType)
        {
            if (concreteAttributeValueType == null)
            {
                return;
            }

            lock (Gate)
            {
                FactoriesByConcreteType.Remove(concreteAttributeValueType);
            }
        }

        private static void RegisterLegacy(AttributeValueType kind, Func<AttributeValue> createDefault)
        {
            AttributeValue sample = createDefault();
            Type concrete = sample.GetType();
            LegacyKindToConcreteType[kind] = concrete;
            FactoriesByConcreteType[concrete] = createDefault;
        }
    }
}
