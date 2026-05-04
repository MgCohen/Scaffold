#nullable enable

using System;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed partial record EntityStateReference(InstanceId EntityId) : Reference
    {
        public static EntityStateReference From(InstanceId entityId)
        {
            if (entityId == null)
            {
                throw new ArgumentNullException(nameof(entityId));
            }

            return new EntityStateReference(entityId);
        }
    }
}
