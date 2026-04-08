using System;
using UnityEngine;

namespace Scaffold.Entities
{
    /// <summary>
    /// Creates <see cref="EntityInstance{TDefinition}"/> and optional <see cref="EntityComponent{TDefinition}"/> hosts bound to a definition.
    /// </summary>
    public static class EntityInstanceFactory
    {
        private static int counter = 0;
        public static EntityInstance<TDefinition> CreateInstance<TDefinition>(TDefinition definition) where TDefinition : EntityDefinition
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var instance = new EntityInstance<TDefinition>();
            instance.Initialize(new InstanceId(counter++), definition);
            return instance;
        }

        public static TEntity CreateOnGameObject<TEntity, TDefinition>(GameObject gameObject, TDefinition definition) where TEntity : EntityComponent<TDefinition> where TDefinition : EntityDefinition
        {
            if (gameObject == null)
            {
                throw new ArgumentNullException(nameof(gameObject));
            }

            TEntity entity = gameObject.AddComponent<TEntity>();
            if (entity == null)
            {
                throw new InvalidOperationException(
                    $"AddComponent failed for {typeof(TEntity).FullName}. The component type must be concrete and loadable.");
            }

            entity.InitializeFromDefinition(new InstanceId(counter++), definition);
            return entity;
        }
    }
}
