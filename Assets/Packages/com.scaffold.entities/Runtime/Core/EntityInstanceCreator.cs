using System;
using UnityEngine;

namespace Scaffold.Entities
{
    public sealed class EntityInstanceCreator<TDefinition> where TDefinition : EntityDefinition
    {
        public EntityInstanceCreator(IInstanceIdGenerator idGenerator)
        {
            this.idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        }

        private readonly IInstanceIdGenerator idGenerator;

        public EntityInstance<TDefinition> Create(TDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var instance = new EntityInstance<TDefinition>();
            instance.Initialize(idGenerator.Next(), definition);
            return instance;
        }

        public TEntity CreateOnGameObject<TEntity>(GameObject gameObject, TDefinition definition) where TEntity : EntityComponent<TDefinition>
        {
            if (gameObject == null)
            {
                throw new ArgumentNullException(nameof(gameObject));
            }

            TEntity entity = gameObject.AddComponent<TEntity>();
            if (entity == null)
            {
                throw new InvalidOperationException($"AddComponent failed for {typeof(TEntity).FullName}. The component type must be concrete and loadable.");
            }

            entity.InitializeFromDefinition(idGenerator.Next(), definition);
            return entity;
        }

        public void InitializeComponent<TEntity>(TEntity entity, TDefinition definition) where TEntity : EntityComponent<TDefinition>
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            entity.InitializeFromDefinition(idGenerator.Next(), definition);
        }
    }
}
