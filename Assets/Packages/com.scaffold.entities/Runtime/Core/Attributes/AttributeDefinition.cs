using System;

namespace Scaffold.Entities
{
    public abstract class AttributeDefinition<TPayload> : IAttributeDefinition<TPayload>
        where TPayload : AttributeValue, new()
    {
        public Type ValueType => typeof(TPayload);

        AttributeValue IAttributeDefinition.CreateDefault() => CreateDefault();

        public virtual TPayload CreateDefault() => new TPayload();

        public bool TryGetPayload(AttributeValue value, out TPayload payload)
        {
            if (value is TPayload typed)
            {
                payload = typed;
                return true;
            }

            payload = null!;
            return false;
        }
    }
}
