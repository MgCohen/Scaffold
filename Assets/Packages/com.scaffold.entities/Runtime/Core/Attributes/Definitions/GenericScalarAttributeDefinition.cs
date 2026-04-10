using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public abstract class GenericScalarAttributeDefinition<T, TAttr> : AttributeDefinitionBase
        where TAttr : AttributeValue, IAttributeValue<T>, new()
    {
        public override Type ConcreteValueType => typeof(TAttr);

        public override AttributeValue CreateDefault() => new TAttr();
    }
}
