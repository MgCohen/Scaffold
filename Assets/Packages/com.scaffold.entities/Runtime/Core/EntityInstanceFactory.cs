using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    /// <summary>
    /// Creates <see cref="EntityInstanceState"/> and optional <see cref="Entity"/> hosts bound to a definition.
    /// </summary>
    public static class EntityInstanceFactory
    {
        public static EntityInstanceState CreateState(EntityDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition.RebuildLookup();
            var state = new EntityInstanceState();
            state.Initialize(InstanceId.New(), definition, new List<EntityModifierEntry>());
            return state;
        }

        public static TEntity CreateOnGameObject<TEntity>(GameObject gameObject, EntityDefinition definition) where TEntity : Entity
        {
            if (gameObject == null)
            {
                throw new ArgumentNullException(nameof(gameObject));
            }

            TEntity entity = gameObject.AddComponent<TEntity>();
            entity.InitializeFromDefinition(definition);
            return entity;
        }
    }
}
