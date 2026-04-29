#nullable enable

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    internal sealed class EntityStateProvider<TDefinition> : AggregateProvider<StateEntity<TDefinition>> where TDefinition : IEntityDefinition
    {
        public EntityStateProvider(InstanceId id, TDefinition definition)
        {
            this.id = id;
            this.definition = definition;
        }

        private readonly InstanceId id;
        private readonly TDefinition definition;

        public override void Wire(IStoreScope scope, IAggregateRebuild rebuild)
        {
            scope.Events.Subscribe<EntityVariableState>(id, (_, _, _) => rebuild.RequestRebuild());
        }

        protected override StateEntity<TDefinition> BuildCore(IStateScope scope)
        {
            var source = scope.Get<EntityVariableState>(id);
            var effective = source.ResolveEffectiveValues(definition);
            return new StateEntity<TDefinition>(id, definition, source.BaseValues, source.ModifierStacks, effective);
        }
    }
}
